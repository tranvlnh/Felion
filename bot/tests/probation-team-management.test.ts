import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import {
  createProbationTeam,
  deactivateProbationTeam,
  editProbationTeam,
} from '#app/db/probation-team-management.js';
import { auditLogs, probationTeams } from '#app/db/schema.js';

type RecordedMutation = Record<string, unknown>;

function createWorkflowDatabase(row?: Record<string, unknown>) {
  const audits: RecordedMutation[] = [];
  const inserts: RecordedMutation[] = [];
  const updates: RecordedMutation[] = [];
  const teamId = row?.id ?? '550e8400-e29b-41d4-a716-446655440000';

  const transaction = {
    select: () => ({
      from: () => ({
        where: () => ({
          limit: async () => row ? [row] : [],
        }),
      }),
    }),
    insert: (table: unknown) => ({
      values: (values: RecordedMutation) => {
        if (table === probationTeams) {
          inserts.push(values);
          return { returning: async () => [{ id: teamId }] };
        }
        if (table === auditLogs) {
          audits.push(values);
          return Promise.resolve();
        }
        throw new Error('Unexpected insert target.');
      },
    }),
    update: () => ({
      set: (values: RecordedMutation) => {
        updates.push(values);
        return {
          where: () => ({
            returning: async () => [{ id: teamId }],
          }),
        };
      },
    }),
  };

  const database = {
    db: {
      transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
    },
  } as unknown as Database;

  return { database, audits, inserts, updates };
}

describe('probation team management workflows', () => {
  it('creates a normalized team and audits the privileged mutation', async () => {
    const { database, audits, inserts } = createWorkflowDatabase();

    await createProbationTeam(database, {
      name: '  Team   Alpha ',
      actorDiscordUserId: '123',
    });

    expect(inserts).toEqual([{ name: 'Team Alpha' }]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: '123',
      action: 'ProbationTeamCreated',
      entityType: 'ProbationTeam',
      metadata: { name: 'Team Alpha' },
    })]);
  });

  it('renames an active team and audits the old and new names', async () => {
    const teamId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: teamId,
      name: 'Team Alpha',
      active: true,
    });

    await editProbationTeam(database, {
      teamId,
      name: ' Team Beta ',
      actorDiscordUserId: '456',
    });

    expect(updates).toEqual([expect.objectContaining({ name: 'Team Beta' })]);
    expect(audits).toEqual([expect.objectContaining({
      action: 'ProbationTeamEdited',
      entityId: teamId,
      metadata: { previous: { name: 'Team Alpha' }, current: { name: 'Team Beta' } },
    })]);
  });

  it('deactivates an active team and records an audit row', async () => {
    const teamId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: teamId,
      name: 'Team Alpha',
      active: true,
    });

    await deactivateProbationTeam(database, { teamId, actorDiscordUserId: '789' });

    expect(updates).toEqual([expect.objectContaining({ active: false })]);
    expect(audits).toEqual([expect.objectContaining({
      action: 'ProbationTeamDeactivated',
      entityId: teamId,
      metadata: { name: 'Team Alpha' },
    })]);
  });

  it('rejects edits to inactive teams before mutation or audit', async () => {
    const teamId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: teamId,
      name: 'Team Alpha',
      active: false,
    });

    await expect(editProbationTeam(database, {
      teamId,
      name: 'Team Beta',
      actorDiscordUserId: '123',
    })).rejects.toThrow('inactive');
    expect(updates).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });
});
