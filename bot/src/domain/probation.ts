import { randomUUID } from 'node:crypto';
import { normalizeFullName, normalizeStudentId } from './member.js';

export type ProbationCandidateStatus = 'Active' | 'Passed' | 'Failed' | 'Inactive';

export type ProbationCandidate = {
  id: string;
  studentId: string;
  fullName: string;
  departmentId: string;
  generationId: string;
  teamId: string | null;
  status: ProbationCandidateStatus;
};

export function createProbationCandidate(input: {
  studentId: string;
  fullName: string;
  departmentId: string;
  generationId: string;
}): ProbationCandidate {
  if (!input.departmentId || !input.generationId) {
    throw new Error('Department and generation are required.');
  }

  return {
    id: randomUUID(),
    studentId: normalizeStudentId(input.studentId),
    fullName: normalizeFullName(input.fullName),
    departmentId: input.departmentId,
    generationId: input.generationId,
    teamId: null,
    status: 'Active',
  };
}

export function assertProbationCandidateTransition(
  currentStatus: ProbationCandidateStatus,
  targetStatus: 'Active' | 'Inactive',
): void {
  if (currentStatus === 'Passed' || currentStatus === 'Failed') {
    throw new Error('A decided ProbationCandidate cannot change lifecycle status.');
  }

  if (currentStatus === targetStatus) {
    throw new Error(`ProbationCandidate is already ${targetStatus}.`);
  }
}

export function assertPeerEvaluationAllowed(
  evaluatorCandidateId: string,
  targetCandidateId: string,
  evaluatorTeamId: string | null,
  targetTeamId: string | null,
): void {
  if (evaluatorCandidateId === targetCandidateId) {
    throw new Error('A candidate cannot evaluate themself.');
  }

  if (!evaluatorTeamId || evaluatorTeamId !== targetTeamId) {
    throw new Error('Peer evaluation requires candidates from the same team.');
  }
}

export function assertMentorEvaluationAllowed(
  mentorMemberId: string,
  mentorTeamIds: readonly string[],
  targetTeamId: string | null,
): void {
  if (!targetTeamId || !mentorTeamIds.includes(targetTeamId)) {
    throw new Error(`Member ${mentorMemberId} is not a mentor of the target candidate's team.`);
  }
}

export function normalizeProbationTeamName(value: string): string {
  const name = value.trim().replace(/\s+/g, ' ');
  if (name.length < 2 || name.length > 100) {
    throw new Error('Probation team name is invalid.');
  }

  return name;
}
