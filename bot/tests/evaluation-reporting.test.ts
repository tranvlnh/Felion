import { describe, expect, it } from 'vitest';
import ExcelJS from 'exceljs';
import type { Database } from '#app/db/client.js';
import { createDrizzleEvaluationPersistence } from '#app/db/evaluations/drizzle-evaluation-persistence.js';
import {
  evaluationPeriods,
  mentorEvaluations,
  peerEvaluations,
  probationCandidates,
} from '#app/db/schema.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';
import { createEvaluationReportWorkbook } from '#app/features/evaluations/discord/evaluation-report-ui.js';

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
    scores: [
      { criterionId: 'criterion-mentor', criterionName: 'Ownership', score: 9 },
      { criterionId: 'criterion-mentor-communication', criterionName: 'Communication', score: 8 },
    ],
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

  it('exports a readable workbook with one summary row per evaluation and raw score rows', async () => {
    const service = createEvaluationService(
      createDrizzleEvaluationPersistence(createReportingDatabase()),
    );
    const report = await service.getRawReport(
      { discordUserId: 'admin-discord-id', position: 'Admin' },
      { periodId },
    );

    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.load(await createEvaluationReportWorkbook(report));
    const summary = workbook.getWorksheet('Summary');
    const raw = workbook.getWorksheet('Raw Scores');
    expect(summary).toBeDefined();
    expect(raw).toBeDefined();
    if (!summary || !raw) throw new Error('Expected report worksheets.');

    const summaryHeaders = summary.getRow(1).values.slice(1).map(String);
    const rawHeaders = raw.getRow(1).values.slice(1).map(String);
    expect(summaryHeaders).toContain('Candidate');
    expect(summaryHeaders).toContain('Evaluator');
    expect(summaryHeaders).toContain('Teamwork');
    expect(summaryHeaders).toContain('Ownership');
    expect(summaryHeaders.some((header) => /(?:_id|uuid|identifier)/i.test(header))).toBe(false);
    expect(rawHeaders.some((header) => /(?:_id|uuid|identifier)/i.test(header))).toBe(false);
    expect(summary.rowCount).toBe(3);
    expect(raw.rowCount).toBe(4);
    expect(summary.getCell(2, 5).value).toBe('Target, Candidate');
    expect(summary.getCell(2, 6).value).toBe('Mentor Evaluator');
    expect(raw.getCell(2, 1).isMerged).toBe(true);
    expect(raw.getCell(2, 1).value).toBe('Fall Review');
    expect(raw.getCell(3, 1).isMerged).toBe(true);
    expect(raw.getCell(3, 1).value).toBe('Fall Review');
    expect(raw.getCell(4, 7).value).toBe('Teamwork');
  });

  it('neutralizes spreadsheet formulas in exported text cells', async () => {
    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.load(await createEvaluationReportWorkbook({
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
    }));
    const summary = workbook.getWorksheet('Summary');
    expect(summary).toBeDefined();
    if (!summary) throw new Error('Expected Summary worksheet.');

    expect(summary.getCell(2, 1).value).toBe("'=FORMULA()");
    expect(summary.getCell(2, 6).value).toBe("'@EVALUATOR");
    expect(summary.getCell(2, 10).value).toBe("'+FORMULA()");
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
