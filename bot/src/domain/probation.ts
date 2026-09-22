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
