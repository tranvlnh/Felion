import { describe, expect, it } from 'vitest';
import {
  assertMentorEvaluationAllowed,
  assertPeerEvaluationAllowed,
  normalizeProbationTeamName,
} from '../src/domain/probation.js';

describe('probation evaluation rules', () => {
  it('rejects self peer evaluation', () => {
    expect(() => assertPeerEvaluationAllowed('candidate', 'candidate', 'team', 'team')).toThrow();
  });

  it('requires the same team for peer evaluation', () => {
    expect(() => assertPeerEvaluationAllowed('one', 'two', 'team-a', 'team-b')).toThrow();
  });

  it('requires the mentor to belong to the target team', () => {
    expect(() => assertMentorEvaluationAllowed('mentor', ['team-a'], 'team-b')).toThrow();
    expect(() => assertMentorEvaluationAllowed('mentor', ['team-a'], 'team-a')).not.toThrow();
  });

  it('normalizes and validates probation team names', () => {
    expect(normalizeProbationTeamName('  Team   Alpha ')).toBe('Team Alpha');
    expect(() => normalizeProbationTeamName(' ')).toThrow('invalid');
    expect(() => normalizeProbationTeamName('x'.repeat(101))).toThrow('invalid');
  });
});
