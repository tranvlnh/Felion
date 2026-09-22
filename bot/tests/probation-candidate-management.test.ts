import { describe, expect, it } from 'vitest';
import type { Database } from '../src/db/client.js';
import {
  assignProbationCandidateTeam,
  createActiveProbationCandidate,
  deactivateProbationCandidate,
  reactivateProbationCandidate,
} from '../src/db/probation-candidate-management.js';
import {
  auditLogs,
  departments,
  discordIdentityLinks,
  generations,
  identityRegistry,
  probationCandidates,
  probationTeams,
} from '../src/db/schema.js';

type RecordedMutation = Record<string, unknown>;

function createWorkflowDatabase(rows: Map<unknown, RecordedMutation[]>) {
  const audits: RecordedMutation[] = [];
  const inserts = new Map<unknown, RecordedMutation[]>();
  const updates: RecordedMutation[] = [];

  const transaction = {
    select: () => ({
      from: (table: unknown) => ({
        where: () => ({
          limit: async () => rows.get(table) ?? [],
        }),
      }),
    }),
    insert: (table: unknown) => ({
      values: async (values: RecordedMutation) => {
        if (table === auditLogs) {
          audits.push(values);
          return;
        }
        const recorded = inserts.get(table) ?? [];
        recorded.push(values);
        inserts.set(table, recorded);
      },
    }),
    update: () => ({
      set: (values: RecordedMutation) => {
        updates.push(values);
        return {
          where: () => ({
            returning: async () => [{ id: '550e8400-e29b-41d4-a716-446655440000' }],
          }),
        };
      },
    }),
  };

  const database = {
    db: {
      transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
    },
  } as unknown as NonNullable<Database>;

  return { database, audits, inserts, updates };
}

const candidateId = '550e8400-e29b-41d4-a716-446655440000';
const teamId = '123e4567-e89b-42d3-a456-426614174000';

describe('probation candidate management workflows', () => {
  it('creates an unassigned candidate, identity registry owner, and audit in one transaction', async () => {
    const rows = new Map<unknown, RecordedMutation[]>([
      [departments, [{ id: 'department-id', name: 'Event', slug: 'event', active: true }]],
      [generations, [{ id: 'generation-id', name: 'Gen 12', active: true }]],
    ]);
    const { database, audits, inserts } = createWorkflowDatabase(rows);

    const createdId = await createActiveProbationCandidate(database, {
      studentId: ' sv-001 ',
      fullName: '  Example   Candidate ',
      department: 'event',
      generation: 'Gen 12',
      actorDiscordUserId: 'admin-user',
    });

    expect(createdId).toEqual(expect.any(String));
    expect(inserts.get(probationCandidates)).toEqual([expect.objectContaining({
      studentId: 'SV-001',
      fullName: 'Example Candidate',
      teamId: null,
      status: 'Active',
    })]);
    expect(inserts.get(identityRegistry)).toEqual([{
      studentId: 'SV-001',
      subjectType: 'ProbationCandidate',
      subjectId: createdId,
    }]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: 'admin-user',
      action: 'ProbationCandidateCreated',
      entityId: createdId,
    })]);
  });

  it('assigns an active candidate to an active team and returns its linked Discord user', async () => {
    const rows = new Map<unknown, RecordedMutation[]>([
      [probationCandidates, [{ id: candidateId, status: 'Active', teamId: null }]],
      [probationTeams, [{ id: teamId }]],
      [discordIdentityLinks, [{ discordUserId: 'candidate-user' }]],
    ]);
    const { database, audits, updates } = createWorkflowDatabase(rows);

    await expect(assignProbationCandidateTeam(database, {
      candidateId,
      teamId,
      actorDiscordUserId: 'admin-user',
    })).resolves.toEqual({ candidateId, discordUserId: 'candidate-user' });

    expect(updates).toEqual([expect.objectContaining({ teamId })]);
    expect(audits).toEqual([expect.objectContaining({
      action: 'ProbationCandidateTeamAssigned',
      metadata: { previousTeamId: null, teamId },
    })]);
  });

  it('deactivates and reactivates only undecided candidates with audit history', async () => {
    const deactivateRows = new Map<unknown, RecordedMutation[]>([
      [probationCandidates, [{ status: 'Active' }]],
      [discordIdentityLinks, [{ discordUserId: 'candidate-user' }]],
    ]);
    const deactivate = createWorkflowDatabase(deactivateRows);

    await expect(deactivateProbationCandidate(deactivate.database, {
      candidateId,
      actorDiscordUserId: 'admin-user',
    })).resolves.toEqual({ candidateId, discordUserId: 'candidate-user' });
    expect(deactivate.updates).toEqual([expect.objectContaining({ status: 'Inactive' })]);
    expect(deactivate.audits).toEqual([expect.objectContaining({
      action: 'ProbationCandidateDeactivated',
      metadata: { previousStatus: 'Active', status: 'Inactive' },
    })]);

    const reactivateRows = new Map<unknown, RecordedMutation[]>([
      [probationCandidates, [{ status: 'Inactive' }]],
      [discordIdentityLinks, []],
    ]);
    const reactivate = createWorkflowDatabase(reactivateRows);
    await expect(reactivateProbationCandidate(reactivate.database, {
      candidateId,
      actorDiscordUserId: 'admin-user',
    })).resolves.toEqual({ candidateId, discordUserId: null });
    expect(reactivate.updates).toEqual([expect.objectContaining({ status: 'Active' })]);
    expect(reactivate.audits[0]).toEqual(expect.objectContaining({
      action: 'ProbationCandidateReactivated',
    }));
  });

  it('rejects lifecycle changes after a PASS or FAIL decision without mutation or audit', async () => {
    const rows = new Map<unknown, RecordedMutation[]>([
      [probationCandidates, [{ status: 'Passed' }]],
    ]);
    const { database, audits, updates } = createWorkflowDatabase(rows);

    await expect(deactivateProbationCandidate(database, {
      candidateId,
      actorDiscordUserId: 'admin-user',
    })).rejects.toThrow('decided');
    expect(updates).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });
});
