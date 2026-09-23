export const evaluationKinds = ['Peer', 'Mentor'] as const;
export type EvaluationKind = (typeof evaluationKinds)[number];

export type EvaluationCriterion = {
  id: string;
  kind: EvaluationKind;
  key: string;
  name: string;
  minScore: number;
  maxScore: number;
  sortOrder: number;
  active: boolean;
};

export type EvaluationScoreSnapshot = {
  criterionId: string;
  criterionName: string;
  score: number;
};

export function normalizeEvaluationNote(rawNote: string | null | undefined): string | null {
  if (rawNote === null || rawNote === undefined) {
    return null;
  }

  const note = rawNote.trim();
  if (note.length === 0) {
    return null;
  }
  if (note.length > 2000) {
    throw new Error('Evaluation note must not exceed 2000 characters.');
  }
  return note;
}

export function normalizeEvaluationPeriodName(rawName: string): string {
  const name = rawName.trim().replace(/\s+/g, ' ');
  if (name.length < 2 || name.length > 100) {
    throw new Error('Evaluation period name must contain between 2 and 100 characters.');
  }
  return name;
}

export function normalizeCriterionName(rawName: string): string {
  const name = rawName.trim().replace(/\s+/g, ' ');
  if (name.length < 2 || name.length > 100) {
    throw new Error('Criterion name must contain between 2 and 100 characters.');
  }
  return name;
}

export function criterionKey(name: string): string {
  return normalizeCriterionName(name)
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

export function assertUniqueActiveCriterionName(
  criteria: readonly EvaluationCriterion[],
  kind: EvaluationKind,
  name: string,
  ignoreId?: string,
): void {
  const normalized = normalizeCriterionName(name).toLocaleLowerCase();
  if (criteria.some((criterion) => criterion.active
    && criterion.kind === kind
    && criterion.id !== ignoreId
    && criterion.name.toLocaleLowerCase() === normalized)) {
    throw new Error(`An active ${kind} criterion with this name already exists.`);
  }
}

export function validateScoreSnapshots(
  criteria: readonly EvaluationCriterion[],
  snapshots: readonly EvaluationScoreSnapshot[],
): void {
  const activeCriteria = criteria.filter((criterion) => criterion.active);
  if (activeCriteria.length === 0) {
    throw new Error('At least one active criterion is required.');
  }
  if (snapshots.length !== activeCriteria.length) {
    throw new Error('Every active criterion must receive exactly one score.');
  }

  const seen = new Set<string>();
  for (const snapshot of snapshots) {
    const criterion = activeCriteria.find((candidate) => candidate.id === snapshot.criterionId);
    if (!criterion || seen.has(snapshot.criterionId)) {
      throw new Error('Evaluation contains an unknown or duplicate criterion.');
    }
    if (!Number.isInteger(snapshot.score) || snapshot.score < criterion.minScore || snapshot.score > criterion.maxScore) {
      throw new Error(`Score for ${criterion.name} must be between ${criterion.minScore} and ${criterion.maxScore}.`);
    }
    seen.add(snapshot.criterionId);
  }
}

export function assertCanDeactivateCriterion(activeCriterionCount: number): void {
  if (activeCriterionCount <= 1) {
    throw new Error('Each evaluation kind must retain at least one active score criterion.');
  }
}
