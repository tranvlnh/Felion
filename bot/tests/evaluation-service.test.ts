import { describe, expect, it, vi } from 'vitest';
import type {
  EvaluationPersistence,
  EvaluationTransaction,
} from '#app/features/evaluations/application/contracts.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';

describe('evaluation application service', () => {
  it('normalizes a criterion and uses the typed actor for its audit', async () => {
    const createCriterion = vi.fn().mockResolvedValue('criterion-id');
    const writeAudit = vi.fn().mockResolvedValue(undefined);
    const transaction = {
      listCriteria: vi.fn().mockResolvedValue([]),
      createCriterion,
      writeAudit,
    } as unknown as EvaluationTransaction;
    const persistence: EvaluationPersistence = {
      transaction: async <T>(work: (value: EvaluationTransaction) => Promise<T>): Promise<T> =>
        work(transaction),
    };
    const service = createEvaluationService(persistence);

    await service.addCriterion(
      { discordUserId: 'admin-discord-id' },
      { kind: 'Peer', name: '  Team   Work  ' },
    );

    expect(createCriterion).toHaveBeenCalledWith({
      kind: 'Peer',
      key: 'team-work',
      name: 'Team Work',
      maxScore: 5,
      sortOrder: 0,
    });
    expect(writeAudit).toHaveBeenCalledWith(expect.objectContaining({
      actorDiscordUserId: 'admin-discord-id',
      action: 'EvaluationCriterionCreated',
      entityType: 'EvaluationCriterion',
      entityId: 'criterion-id',
    }));
  });
});
