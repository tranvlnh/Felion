import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { createDrizzleEvaluationPersistence } from '#app/db/evaluations/drizzle-evaluation-persistence.js';
import { auditLogs, evaluationPeriods } from '#app/db/schema.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';

type RecordedMutation = Record<string, unknown>;

function createWorkflowDatabase(options: {
  period?: Record<string, unknown>;
  insertSucceeds?: boolean;
  updateSucceeds?: boolean;
} = {}) {
  const audits: RecordedMutation[] = [];
  const inserts: RecordedMutation[] = [];
  const updates: RecordedMutation[] = [];
  const periodId = options.period?.id ?? '550e8400-e29b-41d4-a716-446655440000';

  const transaction = {
    select: () => ({
      from: () => ({
        where: () => ({
          limit: async () => options.period ? [options.period] : [],
        }),
      }),
    }),
    insert: (table: unknown) => ({
      values: (values: RecordedMutation) => {
        if (table === evaluationPeriods) {
          inserts.push(values);
          return {
            onConflictDoNothing: () => ({
              returning: async () => options.insertSucceeds === false ? [] : [{ id: periodId }],
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
    update: () => ({
      set: (values: RecordedMutation) => {
        updates.push(values);
        return {
          where: () => ({
            returning: async () => options.updateSucceeds === false ? [] : [{ id: periodId }],
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

describe('evaluation period management workflows', () => {
  it('opens a normalized period and audits the privileged mutation', async () => {
    const { database, audits, inserts } = createWorkflowDatabase();
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await service.openPeriod({ discordUserId: '123' }, {
      name: '  Fall   Review  ',
    });

    expect(inserts).toEqual([{ name: 'Fall Review' }]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: '123',
      action: 'EvaluationPeriodOpened',
      entityType: 'EvaluationPeriod',
      metadata: { name: 'Fall Review' },
    })]);
  });

  it('rejects a duplicate name without writing an audit row', async () => {
    const { database, audits } = createWorkflowDatabase({ insertSucceeds: false });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await expect(service.openPeriod({ discordUserId: '123' }, {
      name: 'Fall Review',
    })).rejects.toThrow('already exists');
    expect(audits).toHaveLength(0);
  });

  it('closes an open period and audits the privileged mutation', async () => {
    const periodId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      period: { id: periodId, name: 'Fall Review', status: 'Open' },
    });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await service.closePeriod({ discordUserId: '456' }, { periodId });

    expect(updates).toEqual([expect.objectContaining({ status: 'Closed', closedAt: expect.any(Date) })]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: '456',
      action: 'EvaluationPeriodClosed',
      entityType: 'EvaluationPeriod',
      entityId: periodId,
      metadata: { name: 'Fall Review' },
    })]);
  });

  it('rejects an already closed period before mutation or audit', async () => {
    const periodId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      period: { id: periodId, name: 'Fall Review', status: 'Closed' },
    });
    const service = createEvaluationService(createDrizzleEvaluationPersistence(database));

    await expect(service.closePeriod({ discordUserId: '456' }, { periodId }))
      .rejects.toThrow('already closed');
    expect(updates).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });
});
