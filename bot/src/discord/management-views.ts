import type {
  CandidateDetail,
  CandidateSummary,
  EvaluationPeriodSummary,
  MemberDetail,
  MemberSummary,
  ReferenceSummary,
  TeamDetail,
  TeamSummary,
} from '#app/db/management-read-model.js';

const MAX_LIST_ITEMS = 25;

export function formatCandidateList(candidates: readonly CandidateSummary[]): string {
  if (candidates.length === 0) return 'Không có ProbationCandidate phù hợp.';
  const lines = candidates.slice(0, MAX_LIST_ITEMS).map((candidate) =>
    `• ${candidate.fullName} — ${candidate.studentId} · ${candidate.status} · ${candidate.teamName ?? 'Chưa có team'}`);
  return formatLines(`ProbationCandidate (${candidates.length} kết quả):`, lines);
}

export function formatCandidateDetail(candidate: CandidateDetail): string {
  return [
    `ProbationCandidate: ${candidate.fullName}`,
    `StudentId: ${candidate.studentId}`,
    `Status: ${candidate.status}`,
    `Department: ${candidate.departmentName}`,
    `Generation: ${candidate.generationName}`,
    `Team: ${candidate.teamName ?? 'Chưa có team'}`,
    `Discord: ${candidate.discordUserId ? `<@${candidate.discordUserId}>` : 'Chưa link'}`,
    `Mentor: ${candidate.mentorNames.length > 0 ? candidate.mentorNames.join(', ') : 'Chưa có mentor'}`,
    `Evaluations: Peer ${candidate.peerEvaluationCount} · Mentor ${candidate.mentorEvaluationCount}`,
  ].join('\n');
}

export function formatTeamList(teams: readonly TeamSummary[]): string {
  if (teams.length === 0) return 'Không có probation team.';
  return formatLines(`Probation teams (${teams.length}):`, teams.map((team) =>
    `• ${team.name} · ${team.active ? 'Active' : 'Inactive'} · ${team.candidateCount} candidates · ${team.mentorCount} mentors`));
}

export function formatTeamDetail(team: TeamDetail): string {
  return formatLines(`Probation team: ${team.name}`, [
    `Status: ${team.active ? 'Active' : 'Inactive'}`,
    `Mentors (${team.mentorCount}): ${team.mentorNames.length > 0 ? team.mentorNames.join(', ') : 'Chưa có mentor'}`,
    `Candidates (${team.candidateCount}):`,
    ...(team.candidates.length > 0
      ? team.candidates.map((candidate) => `• ${candidate.fullName} — ${candidate.studentId} · ${candidate.status}`)
      : ['• Chưa có candidate']),
  ]);
}

export function formatMemberList(members: readonly MemberSummary[]): string {
  if (members.length === 0) return 'Không có Member.';
  return formatLines(`Members (${members.length} kết quả):`, members.map((member) =>
    `• ${member.fullName} — ${member.studentId} · ${member.position} · ${member.status} · ${member.departmentName}`));
}

export function formatMemberDetail(member: MemberDetail): string {
  return [
    `Member: ${member.fullName}`,
    `StudentId: ${member.studentId}`,
    `Email: ${member.clubEmail}`,
    `Position: ${member.position}`,
    `Status: ${member.status}`,
    `Department: ${member.departmentName}`,
    `Generation: ${member.generationName}`,
    `Discord: ${member.discordUserId ? `<@${member.discordUserId}>` : 'Chưa link'}`,
    `Mentor teams: ${member.mentorTeams.length > 0 ? member.mentorTeams.join(', ') : 'Không có'}`,
  ].join('\n');
}

export function formatReferenceList(label: string, references: readonly ReferenceSummary[]): string {
  if (references.length === 0) return `Không có ${label}.`;
  return formatLines(`${label} (${references.length}):`, references.map((reference) =>
    `• ${reference.name}${reference.slug ? ` (${reference.slug})` : ''} · ${reference.active ? 'Active' : 'Inactive'}`));
}

export function formatEvaluationPeriodList(periods: readonly EvaluationPeriodSummary[]): string {
  if (periods.length === 0) return 'Không có evaluation period.';
  return formatLines(`Evaluation periods (${periods.length}):`, periods.map((period) =>
    `• ${period.name} · ${period.status} · Peer ${period.peerCount} · Mentor ${period.mentorCount}`));
}

export function formatEvaluationPeriodDetail(period: EvaluationPeriodSummary): string {
  return [
    `Evaluation period: ${period.name}`,
    `Status: ${period.status}`,
    `Opened: ${period.openedAt.toISOString()}`,
    `Closed: ${period.closedAt?.toISOString() ?? 'Chưa đóng'}`,
    `Peer submissions: ${period.peerCount}`,
    `Mentor submissions: ${period.mentorCount}`,
  ].join('\n');
}

export function formatCriteriaList(
  criteria: readonly { kind: 'Peer' | 'Mentor'; name: string; active: boolean; minScore: number; maxScore: number }[],
): string {
  if (criteria.length === 0) return 'Không có evaluation criterion.';
  return formatLines(`Evaluation criteria (${criteria.length}):`, criteria.map((criterion) =>
    `• ${criterion.kind}: ${criterion.name} · ${criterion.minScore}-${criterion.maxScore} · ${criterion.active ? 'Active' : 'Inactive'}`));
}

function formatLines(header: string, lines: readonly string[]): string {
  const parts = [header];
  for (const line of lines) {
    const next = `${parts.join('\n')}\n${line}`;
    if (next.length > 1900) break;
    parts.push(line);
  }
  if (parts.length - 1 < lines.length) parts.push('… Kết quả còn lại được rút gọn; dùng lệnh view để xem chi tiết.');
  return parts.join('\n');
}
