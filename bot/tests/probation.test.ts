import { describe, expect, it } from 'vitest';
import {
  assertProbationCandidateTransition,
  assertMentorEvaluationAllowed,
  assertPeerEvaluationAllowed,
  createProbationCandidate,
  normalizeProbationTeamName,
} from '#app/domain/probation.js';

describe('probation candidate lifecycle rules', () => {
  it('creates an active unassigned candidate with normalized identity fields', () => {
    const candidate = createProbationCandidate({
      studentId: ' sv-001 ',
      fullName: '  Example   Candidate ',
      departmentId: 'department-id',
      generationId: 'generation-id',
    });

    expect(candidate).toMatchObject({
      studentId: 'SV-001',
      fullName: 'Example Candidate',
      teamId: null,
      status: 'Active',
    });
  });

  it('allows only Active and Inactive lifecycle transitions', () => {
    expect(() => assertProbationCandidateTransition('Active', 'Inactive')).not.toThrow();
    expect(() => assertProbationCandidateTransition('Inactive', 'Active')).not.toThrow();
    expect(() => assertProbationCandidateTransition('Active', 'Active')).toThrow('already Active');
    expect(() => assertProbationCandidateTransition('Passed', 'Inactive')).toThrow('decided');
    expect(() => assertProbationCandidateTransition('Failed', 'Active')).toThrow('decided');
  });
});

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
