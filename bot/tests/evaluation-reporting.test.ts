import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { createDrizzleEvaluationPersistence } from '#app/db/evaluations/drizzle-evaluation-persistence.js';
import {
  evaluationPeriods,
  mentorEvaluations,
  peerEvaluations,
  probationCandidates,
} from '#app/db/schema.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';
import { createEvaluationReportCsv } from '#app/features/evaluations/discord/evaluation-report-ui.js';

const periodId = '550e8400-e29b-41d4-a716-446655440000';
const candidateId = '550e8400-e29b-41d4-a716-446655440001';

function createReportingDatabase(): Database {
  const period = { id: periodId, name: 'Fall Review', status: 'Closed' };
  const candidate = { id: candidateId, fullName: 'Target, Candidate', teamId: null };
  const peerRows = [{
    id: '550e8400-e29b-41d4-a716-446655440010',
    periodId,
    evaluatorId: '550e8400-e29b-41d4-a716-446655440011',
    evaluatorName: 'Peer Evaluator',
    targetCandidateId: candidateId,
    targetName: 'Target, Candidate',
    scores: [{ criterionId: 'criterion-peer', criterionName: 'Teamwork', score: 5 }],
    note: 'Strong, reliable work.',
    submittedAt: new Date('2026-09-23T10:00:00.000Z'),
  }];
  const mentorRows = [{
    id: '550e8400-e29b-41d4-a716-446655440020',
    periodId,
    evaluatorId: '550e8400-e29b-41d4-a716-446655440021',
    evaluatorName: 'Mentor Evaluator',
    targetCandidateId: candidateId,
    targetName: 'Target, Candidate',
    scores: [{ criterionId: 'criterion-mentor', criterionName: 'Ownership', score: 9 }],
    note: 'Ready for more responsibility.\nConsistent delivery.',
    submittedAt: new Date('2026-09-23T09:00:00.000Z'),
  }];

  const transaction = {
    select: () => {
      let source: unknown;
      const query = {
        from: (table: unknown) => {
          source = table;
          return query;
        },
        where: () => query,
        limit: async () => {
          if (source === evaluationPeriods) return [period];
          if (source === probationCandidates) return [candidate];
          return [];
        },
        orderBy: async () => {
          if (source === peerEvaluations) return peerRows;
          if (source === mentorEvaluations) return mentorRows;
          return [];
        },
      };
      return query;
    },
  };
  return {
    db: {
      transaction: async <T>(work: (value: typeof transaction) => Promise<T>): Promise<T> =>
        work(transaction),
    },
  } as unknown as Database;
}

describe('raw evaluation reporting', () => {
  it('returns Peer and Mentor rows with evaluator identity in chronological order', async () => {
    const service = createEvaluationService(
      createDrizzleEvaluationPersistence(createReportingDatabase()),
    );

    const report = await service.getRawReport(
      { discordUserId: 'core-discord-id', position: 'Core' },
      { periodId, targetCandidateId: candidateId },
    );

    expect(report.period).toEqual({ id: periodId, name: 'Fall Review', status: 'Closed' });
    expect(report.target).toEqual({ id: candidateId, fullName: 'Target, Candidate' });
    expect(report.evaluations.map(({ kind, evaluatorName }) => ({ kind, evaluatorName })))
      .toEqual([
        { kind: 'Mentor', evaluatorName: 'Mentor Evaluator' },
        { kind: 'Peer', evaluatorName: 'Peer Evaluator' },
      ]);
  });

  it('exports one CSV row per score with evaluator identity and escaped raw text', async () => {
    const service = createEvaluationService(
      createDrizzleEvaluationPersistence(createReportingDatabase()),
    );
    const report = await service.getRawReport(
      { discordUserId: 'admin-discord-id', position: 'Admin' },
      { periodId },
    );

    const csv = createEvaluationReportCsv(report);

    expect(csv.startsWith('\uFEFFperiod_id,')).toBe(true);
    expect(csv).toContain('Mentor Evaluator');
    expect(csv).toContain('Peer Evaluator');
    expect(csv).toContain('"Target, Candidate"');
    expect(csv).toContain('"Ready for more responsibility.\nConsistent delivery."');
  });

  it('neutralizes spreadsheet formulas in exported text cells', () => {
    const csv = createEvaluationReportCsv({
      period: { id: periodId, name: '=FORMULA()', status: 'Closed' },
      target: null,
      evaluations: [{
        id: 'evaluation-id',
        kind: 'Peer',
        periodId,
        evaluatorId: 'evaluator-id',
        evaluatorName: '@EVALUATOR',
        targetCandidateId: candidateId,
        targetName: 'Target',
        scores: [{ criterionId: 'criterion-id', criterionName: 'Quality', score: 5 }],
        note: '+FORMULA()',
        submittedAt: new Date('2026-09-23T10:00:00.000Z'),
      }],
    });

    expect(csv).not.toContain(',=FORMULA(),');
    expect(csv).toContain(",'=FORMULA(),");
    expect(csv).toContain(",'@EVALUATOR,");
    expect(csv).toContain(",'+FORMULA()\r\n");
  });

  it('applies the optional evaluation-kind filter to the candidate view', async () => {
    const service = createEvaluationService(
      createDrizzleEvaluationPersistence(createReportingDatabase()),
    );

    const report = await service.getRawReport(
      { discordUserId: 'core-discord-id', position: 'Core' },
      { periodId, targetCandidateId: candidateId, kind: 'Peer' },
    );

    expect(report.evaluations).toHaveLength(1);
    expect(report.evaluations[0]).toMatchObject({
      kind: 'Peer',
      evaluatorName: 'Peer Evaluator',
    });
  });
});
