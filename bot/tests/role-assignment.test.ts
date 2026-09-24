import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { assignExplicitDiscordRole } from '#app/db/reference-management.js';
import {
  auditLogs,
  discordIdentityLinks,
  discordRoleAssignments,
  members,
} from '#app/db/schema.js';

type RecordedMutation = Record<string, unknown>;

const memberId = '550e8400-e29b-41d4-a716-446655440000';
const roleId = '123456789012345678';

function createDatabase(options: { activeMember?: boolean; alreadyAssigned?: boolean } = {}) {
  const assignments: RecordedMutation[] = [];
  const audits: RecordedMutation[] = [];
  const transaction = {
    select: (selection: Record<string, unknown>) => ({
      from: (table: unknown) => ({
        where: () => ({
          limit: async () => {
            if (table === discordIdentityLinks) return [{ subjectType: 'Member', subjectId: memberId }];
            if (table === members) return options.activeMember === false ? [] : [{ id: memberId }];
            throw new Error(`Unexpected select: ${Object.keys(selection).join(',')}`);
          },
        }),
      }),
    }),
    insert: (table: unknown) => ({
      values: (values: RecordedMutation) => {
        if (table === discordRoleAssignments) {
          assignments.push(values);
          return {
            onConflictDoNothing: () => ({
              returning: async () => options.alreadyAssigned ? [] : [{ subjectId: memberId }],
            }),
          };
        }
        if (table === auditLogs) {
          audits.push(values);
          return Promise.resolve();
        }
        throw new Error('Unexpected insert target.');
      },
    }),
  };

  const database = {
    db: {
      transaction: async <T>(work: (value: typeof transaction) => Promise<T>) => work(transaction),
    },
  } as unknown as Database;

  return { database, assignments, audits };
}

describe('explicit Discord role assignment', () => {
  it('stores and audits an explicit role assignment for an active linked Member', async () => {
    const { database, assignments, audits } = createDatabase();

    await assignExplicitDiscordRole(database, {
      discordUserId: 'discord-member',
      discordRoleId: roleId,
      actorDiscordUserId: 'admin-user',
    });

    expect(assignments).toEqual([{
      subjectType: 'Member',
      subjectId: memberId,
      discordRoleId: roleId,
    }]);
    expect(audits).toEqual([expect.objectContaining({
      action: 'DiscordRoleExplicitlyAssigned',
      entityType: 'Member',
      entityId: memberId,
      metadata: { discordUserId: 'discord-member', discordRoleId: roleId },
    })]);
  });

  it('rejects inactive Members and duplicate assignments without auditing', async () => {
    const inactive = createDatabase({ activeMember: false });
    await expect(assignExplicitDiscordRole(inactive.database, {
      discordUserId: 'discord-member',
      discordRoleId: roleId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('not active');

    const duplicate = createDatabase({ alreadyAssigned: true });
    await expect(assignExplicitDiscordRole(duplicate.database, {
      discordUserId: 'discord-member',
      discordRoleId: roleId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('already explicitly assigned');
    expect(duplicate.audits).toHaveLength(0);
  });
});
