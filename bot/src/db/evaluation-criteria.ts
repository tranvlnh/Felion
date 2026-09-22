import { and, eq } from 'drizzle-orm';
import {
  assertUniqueActiveCriterionName,
  criterionKey,
  normalizeCriterionName,
  assertCanDeactivateCriterion,
  type EvaluationCriterion,
  type EvaluationKind,
} from '../domain/evaluation.js';
import type { Database } from './client.js';
import { auditLogs, evaluationCriteria } from './schema.js';

const maxScoreByKind: Record<EvaluationKind, number> = { Peer: 5, Mentor: 10 };

function toDomain(row: typeof evaluationCriteria.$inferSelect): EvaluationCriterion {
  return {
    id: row.id,
    kind: row.kind,
    key: row.key,
    name: row.name,
    minScore: row.minScore,
    maxScore: row.maxScore,
    sortOrder: row.sortOrder,
    active: row.active,
  };
}

export async function addEvaluationCriterion(
  database: NonNullable<Database>,
  input: { kind: EvaluationKind; name: string; actorDiscordUserId: string },
): Promise<void> {
  const name = normalizeCriterionName(input.name);
  const key = criterionKey(name);

  await database.db.transaction(async (transaction) => {
    const rows = await transaction.select().from(evaluationCriteria).where(eq(evaluationCriteria.kind, input.kind));
    const existing = rows.find((row) => row.key === key);
    assertUniqueActiveCriterionName(rows.map(toDomain), input.kind, name, existing?.id);

    let criterionId: string;
    let action: string;
    if (existing) {
      const reactivated = (await transaction.update(evaluationCriteria)
        .set({ name, active: true, updatedAt: new Date() })
        .where(eq(evaluationCriteria.id, existing.id))
        .returning({ id: evaluationCriteria.id }))[0];
      if (!reactivated) throw new Error('Unable to reactivate evaluation criterion.');
      criterionId = reactivated.id;
      action = 'EvaluationCriterionReactivated';
    } else {
      const created = (await transaction.insert(evaluationCriteria).values({
        kind: input.kind,
        key,
        name,
        maxScore: maxScoreByKind[input.kind],
        sortOrder: rows.length,
      }).returning({ id: evaluationCriteria.id }))[0];
      if (!created) throw new Error('Unable to create evaluation criterion.');
      criterionId = created.id;
      action = 'EvaluationCriterionCreated';
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action,
      entityType: 'EvaluationCriterion',
      entityId: criterionId,
      metadata: { kind: input.kind, name, key },
    });
  });
}

export async function renameEvaluationCriterion(
  database: NonNullable<Database>,
  input: { criterionId: string; name: string; actorDiscordUserId: string },
): Promise<void> {
  const name = normalizeCriterionName(input.name);

  await database.db.transaction(async (transaction) => {
    const criterion = (await transaction.select().from(evaluationCriteria)
      .where(eq(evaluationCriteria.id, input.criterionId)).limit(1))[0];
    if (!criterion || !criterion.active) throw new Error('Active evaluation criterion was not found.');

    const rows = await transaction.select().from(evaluationCriteria).where(eq(evaluationCriteria.kind, criterion.kind));
    assertUniqueActiveCriterionName(rows.map(toDomain), criterion.kind, name, criterion.id);

    await transaction.update(evaluationCriteria)
      .set({ name, updatedAt: new Date() })
      .where(eq(evaluationCriteria.id, criterion.id));

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'EvaluationCriterionRenamed',
      entityType: 'EvaluationCriterion',
      entityId: criterion.id,
      metadata: { kind: criterion.kind, previousName: criterion.name, name },
    });
  });
}

export async function deactivateEvaluationCriterion(
  database: NonNullable<Database>,
  input: { criterionId: string; actorDiscordUserId: string },
): Promise<void> {
  await database.db.transaction(async (transaction) => {
    const criterion = (await transaction.select().from(evaluationCriteria)
      .where(eq(evaluationCriteria.id, input.criterionId)).limit(1))[0];
    if (!criterion || !criterion.active) throw new Error('Active evaluation criterion was not found.');

    const activeRows = await transaction.select({ id: evaluationCriteria.id }).from(evaluationCriteria).where(and(
      eq(evaluationCriteria.kind, criterion.kind),
      eq(evaluationCriteria.active, true),
    ));
    assertCanDeactivateCriterion(activeRows.length);

    await transaction.update(evaluationCriteria)
      .set({ active: false, updatedAt: new Date() })
      .where(eq(evaluationCriteria.id, criterion.id));

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'EvaluationCriterionDeactivated',
      entityType: 'EvaluationCriterion',
      entityId: criterion.id,
      metadata: { kind: criterion.kind, name: criterion.name },
    });
  });
}
