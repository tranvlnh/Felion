import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { createDrizzleEvaluationPersistence } from '#app/db/evaluations/drizzle-evaluation-persistence.js';
import {
  auditLogs,
  discordIdentityLinks,
  evaluationCriteria,
  evaluationPeriods,
  mentorEvaluations,
  members,
  peerEvaluations,
  probationCandidates,
  teamMentors,
} from '#app/db/schema.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';

type RecordedMutation = Record<string, unknown>;

const periodId = '550e8400-e29b-41d4-a716-446655440000';
const evaluatorCandidateId = '550e8400-e29b-41d4-a716-446655440001';
const targetCandidateId = '550e8400-e29b-41d4-a716-446655440002';
const mentorMemberId = '550e8400-e29b-41d4-a716-446655440003';
const teamId = '550e8400-e29b-41d4-a716-446655440004';

function createSubmissionDatabase(options: {
  kind: 'Peer' | 'Mentor';
  insertSucceeds?: boolean;
  targetTeamId?: string | null;
}): { database: Database; inserts: RecordedMutation[]; audits: RecordedMutation[] } {
  const inserts: RecordedMutation[] = [];
  const audits: RecordedMutation[] = [];
  const criteria = [
    {
      id: '550e8400-e29b-41d4-a716-446655440010',
      kind: options.kind,
      key: 'quality',
      name: 'Quality',
      minScore: 1,
      maxScore: options.kind === 'Peer' ? 5 : 10,
      sortOrder: 0,
      active: true,
    },
    {
      id: '550e8400-e29b-41d4-a716-446655440011',
      kind: options.kind,
      key: 'communication',
      name: 'Communication',
      minScore: 1,
      maxScore: options.kind === 'Peer' ? 5 : 10,
      sortOrder: 1,
      active: true,
    },
  ];
  const target = {
    id: targetCandidateId,
    fullName: 'Target Candidate',
    teamId: options.targetTeamId === undefined ? teamId : options.targetTeamId,
  };
  const period = { id: periodId, status: 'Open' };
  const peerEvaluator = { id: evaluatorCandidateId, fullName: 'Peer Evaluator', teamId };
  const mentorEvaluator = { id: mentorMemberId, fullName: 'Mentor Evaluator' };
  const mentorTeams = [{ teamId }];

  const transaction = {
    select: () => {
      let source: unknown;
      const query = {
        from: (table: unknown) => {
          source = table;
          return query;
        },
        innerJoin: () => query,
        where: () => source === teamMentors ? Promise.resolve(mentorTeams) : query,
        orderBy: async () => source === evaluationCriteria ? criteria : [],
        limit: async () => {
          if (source === evaluationPeriods) return [period];
          if (source === probationCandidates) return [target];
          if (source === discordIdentityLinks) return [options.kind === 'Peer' ? peerEvaluator : mentorEvaluator];
          if (source === members) return [mentorEvaluator];
          return [];
        },
      };
      return query;
    },
    insert: (table: unknown) => ({
      values: (values: RecordedMutation) => {
        if (table === auditLogs) {
          audits.push(values);
          return Promise.resolve();
        }
        inserts.push(values);
        return {
          onConflictDoNothing: () => ({
            returning: async () => options.insertSucceeds === false ? [] : [{ id: '550e8400-e29b-41d4-a716-446655440099' }],
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

  return { database, inserts, audits };
}

describe('evaluation submission workflows', () => {
  it('submits a Peer evaluation with score snapshots and an audit row', async () => {
    const { database, inserts, audits } = createSubmissionDatabase({ kind: 'Peer' });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await service.submitPeerEvaluation({
      periodId,
      targetCandidateId,
      actorDiscordUserId: 'discord-peer',
      scores: [
        { criterionId: '550e8400-e29b-41d4-a716-446655440010', score: 4 },
        { criterionId: '550e8400-e29b-41d4-a716-446655440011', score: 5 },
      ],
      note: '  Strong collaboration.  ',
    });

    expect(inserts).toEqual([expect.objectContaining({
      periodId,
      evaluatorCandidateId,
      targetCandidateId,
      note: 'Strong collaboration.',
      scores: [
        { criterionId: '550e8400-e29b-41d4-a716-446655440010', criterionName: 'Quality', score: 4 },
        { criterionId: '550e8400-e29b-41d4-a716-446655440011', criterionName: 'Communication', score: 5 },
      ],
    })]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: 'discord-peer',
      action: 'PeerEvaluationSubmitted',
      entityType: 'PeerEvaluation',
      metadata: expect.objectContaining({ periodId, evaluatorId: evaluatorCandidateId, targetCandidateId }),
    })]);
  });

  it('allows only a mentor of the target team to submit a Mentor evaluation', async () => {
    const { database } = createSubmissionDatabase({ kind: 'Mentor' });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    const context = await service.getSubmissionContext({
      kind: 'Mentor',
      periodId,
      targetCandidateId,
      actorDiscordUserId: 'discord-mentor',
    });

    expect(context).toEqual(expect.objectContaining({
      kind: 'Mentor',
      evaluatorId: mentorMemberId,
      targetCandidateId,
      criteria: expect.arrayContaining([
        expect.objectContaining({ name: 'Quality' }),
      ]),
    }));
  });

  it('rejects duplicate submissions without writing an audit row', async () => {
    const { database, audits } = createSubmissionDatabase({ kind: 'Peer', insertSucceeds: false });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await expect(service.submitPeerEvaluation({
      periodId,
      targetCandidateId,
      actorDiscordUserId: 'discord-peer',
      scores: [
        { criterionId: '550e8400-e29b-41d4-a716-446655440010', score: 4 },
        { criterionId: '550e8400-e29b-41d4-a716-446655440011', score: 5 },
      ],
    })).rejects.toThrow('already been submitted');
    expect(audits).toHaveLength(0);
  });

  it('rejects a Peer target outside the evaluator team', async () => {
    const { database } = createSubmissionDatabase({
      kind: 'Peer',
      targetTeamId: '550e8400-e29b-41d4-a716-446655440005',
    });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await expect(service.getSubmissionContext({
      kind: 'Peer',
      periodId,
      targetCandidateId,
      actorDiscordUserId: 'discord-peer',
    })).rejects.toThrow('same team');
  });
});
