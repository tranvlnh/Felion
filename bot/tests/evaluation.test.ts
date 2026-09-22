import { describe, expect, it } from 'vitest';
import {
  assertUniqueActiveCriterionName,
  assertCanDeactivateCriterion,
  criterionKey,
  normalizeCriterionName,
  validateScoreSnapshots,
  type EvaluationCriterion,
} from '../src/domain/evaluation.js';

const criteria: EvaluationCriterion[] = [
  { id: 'one', kind: 'Peer', key: 'contribution', name: 'Contribution', minScore: 1, maxScore: 5, sortOrder: 0, active: true },
  { id: 'two', kind: 'Peer', key: 'communication', name: 'Communication', minScore: 1, maxScore: 5, sortOrder: 1, active: true },
];

describe('configurable evaluation criteria', () => {
  it('normalizes names and creates stable keys', () => {
    expect(normalizeCriterionName('  Task   Completion ')).toBe('Task Completion');
    expect(criterionKey('Task Completion')).toBe('task-completion');
  });

  it('rejects duplicate active names but permits a rename of the same row', () => {
    expect(() => assertUniqueActiveCriterionName(criteria, 'Peer', ' contribution ')).toThrow();
    expect(() => assertUniqueActiveCriterionName(criteria, 'Peer', 'Contribution', 'one')).not.toThrow();
  });

  it('requires exactly one valid score for every active criterion', () => {
    expect(() => validateScoreSnapshots(criteria, [
      { criterionId: 'one', criterionName: 'Contribution', score: 4 },
      { criterionId: 'two', criterionName: 'Communication', score: 5 },
    ])).not.toThrow();
    expect(() => validateScoreSnapshots(criteria, [
      { criterionId: 'one', criterionName: 'Contribution', score: 6 },
      { criterionId: 'two', criterionName: 'Communication', score: 5 },
    ])).toThrow();
  });

  it('does not allow removing the last active score criterion', () => {
    expect(() => assertCanDeactivateCriterion(1)).toThrow();
    expect(() => assertCanDeactivateCriterion(2)).not.toThrow();
  });
});
