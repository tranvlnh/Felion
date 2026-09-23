import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { assignProbationTeamMentor } from '#app/db/probation-team-management.js';
import { auditLogs, members, probationTeams, teamMentors } from '#app/db/schema.js';

type RecordedMutation = Record<string, unknown>;

function createWorkflowDatabase(options: {
  activeTeam?: boolean;
  activeMember?: boolean;
  assignmentExists?: boolean;
} = {}) {
  const activeTeam = options.activeTeam ?? true;
  const activeMember = options.activeMember ?? true;
  const assignmentExists = options.assignmentExists ?? false;
  const audits: RecordedMutation[] = [];
  const assignments: RecordedMutation[] = [];

  const transaction = {
    select: () => ({
      from: (table: unknown) => ({
        where: () => ({
          limit: async () => {
            if (table === probationTeams) {
              return activeTeam ? [{ id: teamId }] : [];
            }
            if (table === members) {
              return activeMember ? [{ id: memberId }] : [];
            }
            throw new Error('Unexpected select target.');
          },
        }),
      }),
    }),
    insert: (table: unknown) => ({
      values: (values: RecordedMutation) => {
        if (table === teamMentors) {
          assignments.push(values);
          return {
            onConflictDoNothing: () => ({
              returning: async () => assignmentExists ? [] : [{ teamId }],
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
      transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
    },
  } as unknown as Database;

  return { database, assignments, audits };
}

const teamId = '550e8400-e29b-41d4-a716-446655440000';
const memberId = '123e4567-e89b-42d3-a456-426614174000';

describe('probation team mentor assignment', () => {
  it('assigns an active Member to an active team and audits the mutation', async () => {
    const { database, assignments, audits } = createWorkflowDatabase();

    await assignProbationTeamMentor(database, {
      teamId,
      memberId,
      actorDiscordUserId: 'admin-user',
    });

    expect(assignments).toEqual([{ teamId, memberId }]);
    expect(audits).toEqual([{
      actorDiscordUserId: 'admin-user',
      action: 'ProbationTeamMentorAssigned',
      entityType: 'ProbationTeam',
      entityId: teamId,
      metadata: { memberId },
    }]);
  });

  it('rejects assignment when the probation team is not active', async () => {
    const { database, assignments, audits } = createWorkflowDatabase({ activeTeam: false });

    await expect(assignProbationTeamMentor(database, {
      teamId,
      memberId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('active probation team');

    expect(assignments).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });

  it('rejects assignment when the Member is not active', async () => {
    const { database, assignments, audits } = createWorkflowDatabase({ activeMember: false });

    await expect(assignProbationTeamMentor(database, {
      teamId,
      memberId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('active Member');

    expect(assignments).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });

  it('rejects an existing mentor assignment without writing an audit row', async () => {
    const { database, assignments, audits } = createWorkflowDatabase({ assignmentExists: true });

    await expect(assignProbationTeamMentor(database, {
      teamId,
      memberId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('already assigned');

    expect(assignments).toEqual([{ teamId, memberId }]);
    expect(audits).toHaveLength(0);
  });
});
