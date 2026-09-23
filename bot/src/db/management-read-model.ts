import { and, asc, count, desc, eq } from 'drizzle-orm';
import type { Database } from './client.js';
import {
  departments,
  discordIdentityLinks,
  evaluationCriteria,
  evaluationPeriods,
  generations,
  members,
  mentorEvaluations,
  peerEvaluations,
  probationCandidates,
  probationTeams,
  teamMentors,
} from './schema.js';

export type CandidateStatus = 'Active' | 'Inactive' | 'Passed' | 'Failed';

export type CandidateSummary = {
  id: string;
  studentId: string;
  fullName: string;
  status: CandidateStatus;
  departmentName: string;
  generationName: string;
  teamId: string | null;
  teamName: string | null;
  discordUserId: string | null;
};

export type CandidateDetail = CandidateSummary & {
  mentorNames: string[];
  peerEvaluationCount: number;
  mentorEvaluationCount: number;
};

export async function listCandidateSummaries(
  database: Database,
  input: { status?: CandidateStatus; teamId?: string; candidateId?: string } = {},
): Promise<CandidateSummary[]> {
  const filters = [];
  if (input.status) filters.push(eq(probationCandidates.status, input.status));
  if (input.teamId) filters.push(eq(probationCandidates.teamId, input.teamId));
  if (input.candidateId) filters.push(eq(probationCandidates.id, input.candidateId));

  const rows = await database.db
    .select({
      id: probationCandidates.id,
      studentId: probationCandidates.studentId,
      fullName: probationCandidates.fullName,
      status: probationCandidates.status,
      departmentName: departments.name,
      generationName: generations.name,
      teamId: probationTeams.id,
      teamName: probationTeams.name,
      discordUserId: discordIdentityLinks.discordUserId,
    })
    .from(probationCandidates)
    .innerJoin(departments, eq(departments.id, probationCandidates.departmentId))
    .innerJoin(generations, eq(generations.id, probationCandidates.generationId))
    .leftJoin(probationTeams, eq(probationTeams.id, probationCandidates.teamId))
    .leftJoin(discordIdentityLinks, and(
      eq(discordIdentityLinks.subjectType, 'ProbationCandidate'),
      eq(discordIdentityLinks.subjectId, probationCandidates.id),
    ))
    .where(filters.length > 0 ? and(...filters) : undefined)
    .orderBy(asc(probationCandidates.status), asc(probationCandidates.fullName))
    .limit(25);

  return rows;
}

export async function getCandidateDetail(
  database: Database,
  candidateId: string,
): Promise<CandidateDetail | null> {
  const candidates = await listCandidateSummaries(database, { candidateId });
  const candidate = candidates[0];
  if (!candidate) return null;

  const [mentorRows, peerCountRows, mentorCountRows] = await Promise.all([
    candidate.teamId
      ? database.db
        .select({ fullName: members.fullName })
        .from(teamMentors)
        .innerJoin(members, eq(members.id, teamMentors.memberId))
        .where(and(eq(teamMentors.teamId, candidate.teamId), eq(members.status, 'Active')))
        .orderBy(asc(members.fullName))
      : Promise.resolve([]),
    database.db
      .select({ value: count() })
      .from(peerEvaluations)
      .where(eq(peerEvaluations.targetCandidateId, candidateId)),
    database.db
      .select({ value: count() })
      .from(mentorEvaluations)
      .where(eq(mentorEvaluations.targetCandidateId, candidateId)),
  ]);

  return {
    ...candidate,
    mentorNames: mentorRows.map((row) => row.fullName),
    peerEvaluationCount: Number(peerCountRows[0]?.value ?? 0),
    mentorEvaluationCount: Number(mentorCountRows[0]?.value ?? 0),
  };
}

export type TeamSummary = {
  id: string;
  name: string;
  active: boolean;
  mentorCount: number;
  candidateCount: number;
};

export type TeamDetail = TeamSummary & {
  mentorNames: string[];
  candidates: CandidateSummary[];
};

export async function listTeamSummaries(database: Database): Promise<TeamSummary[]> {
  const teams = await database.db
    .select({ id: probationTeams.id, name: probationTeams.name, active: probationTeams.active })
    .from(probationTeams)
    .orderBy(desc(probationTeams.active), asc(probationTeams.name));

  return Promise.all(teams.map(async (team) => {
    const [mentorCount, candidateCount] = await Promise.all([
      database.db.select({ value: count() }).from(teamMentors).where(eq(teamMentors.teamId, team.id)),
      database.db.select({ value: count() }).from(probationCandidates).where(eq(probationCandidates.teamId, team.id)),
    ]);
    return {
      ...team,
      mentorCount: Number(mentorCount[0]?.value ?? 0),
      candidateCount: Number(candidateCount[0]?.value ?? 0),
    };
  }));
}

export async function getTeamDetail(database: Database, teamId: string): Promise<TeamDetail | null> {
  const teams = (await database.db
    .select({ id: probationTeams.id, name: probationTeams.name, active: probationTeams.active })
    .from(probationTeams)
    .where(eq(probationTeams.id, teamId))
    .limit(1));
  const team = teams[0];
  if (!team) return null;

  const [summary, mentors, candidates] = await Promise.all([
    listTeamSummaries(database),
    database.db
      .select({ fullName: members.fullName })
      .from(teamMentors)
      .innerJoin(members, eq(members.id, teamMentors.memberId))
      .where(eq(teamMentors.teamId, teamId))
      .orderBy(asc(members.fullName)),
    listCandidateSummaries(database, { teamId }),
  ]);
  const teamSummary = summary.find((item) => item.id === teamId);

  return {
    ...team,
    mentorCount: teamSummary?.mentorCount ?? 0,
    candidateCount: teamSummary?.candidateCount ?? 0,
    mentorNames: mentors.map((mentor) => mentor.fullName),
    candidates,
  };
}

export type MemberSummary = {
  id: string;
  studentId: string;
  fullName: string;
  clubEmail: string;
  position: 'Admin' | 'Core' | 'Member';
  status: 'Active' | 'Inactive';
  departmentName: string;
  generationName: string;
  discordUserId: string | null;
};

export type MemberDetail = MemberSummary & { mentorTeams: string[] };

export async function listMemberSummaries(database: Database): Promise<MemberSummary[]> {
  return database.db
    .select({
      id: members.id,
      studentId: members.studentId,
      fullName: members.fullName,
      clubEmail: members.clubEmail,
      position: members.position,
      status: members.status,
      departmentName: departments.name,
      generationName: generations.name,
      discordUserId: discordIdentityLinks.discordUserId,
    })
    .from(members)
    .innerJoin(departments, eq(departments.id, members.departmentId))
    .innerJoin(generations, eq(generations.id, members.generationId))
    .leftJoin(discordIdentityLinks, and(
      eq(discordIdentityLinks.subjectType, 'Member'),
      eq(discordIdentityLinks.subjectId, members.id),
    ))
    .orderBy(desc(members.status), asc(members.fullName))
    .limit(25);
}

export async function getMemberDetail(database: Database, memberId: string): Promise<MemberDetail | null> {
  const membersList = await database.db
    .select({
      id: members.id,
      studentId: members.studentId,
      fullName: members.fullName,
      clubEmail: members.clubEmail,
      position: members.position,
      status: members.status,
      departmentName: departments.name,
      generationName: generations.name,
      discordUserId: discordIdentityLinks.discordUserId,
    })
    .from(members)
    .innerJoin(departments, eq(departments.id, members.departmentId))
    .innerJoin(generations, eq(generations.id, members.generationId))
    .leftJoin(discordIdentityLinks, and(
      eq(discordIdentityLinks.subjectType, 'Member'),
      eq(discordIdentityLinks.subjectId, members.id),
    ))
    .where(eq(members.id, memberId))
    .limit(1);
  const member = membersList[0];
  if (!member) return null;

  const teams = await database.db
    .select({ name: probationTeams.name })
    .from(teamMentors)
    .innerJoin(probationTeams, eq(probationTeams.id, teamMentors.teamId))
    .where(eq(teamMentors.memberId, memberId))
    .orderBy(asc(probationTeams.name));

  return { ...member, mentorTeams: teams.map((team) => team.name) };
}

export type ReferenceSummary = { id: string; name: string; active: boolean; slug?: string };

export async function listDepartmentSummaries(database: Database): Promise<ReferenceSummary[]> {
  return database.db
    .select({ id: departments.id, name: departments.name, active: departments.active, slug: departments.slug })
    .from(departments)
    .orderBy(desc(departments.active), asc(departments.name));
}

export async function listGenerationSummaries(database: Database): Promise<ReferenceSummary[]> {
  return database.db
    .select({ id: generations.id, name: generations.name, active: generations.active })
    .from(generations)
    .orderBy(desc(generations.active), asc(generations.name));
}

export type EvaluationPeriodSummary = {
  id: string;
  name: string;
  status: 'Open' | 'Closed';
  openedAt: Date;
  closedAt: Date | null;
  peerCount: number;
  mentorCount: number;
};

export async function listEvaluationPeriodSummaries(database: Database): Promise<EvaluationPeriodSummary[]> {
  const periods = await database.db
    .select({
      id: evaluationPeriods.id,
      name: evaluationPeriods.name,
      status: evaluationPeriods.status,
      openedAt: evaluationPeriods.openedAt,
      closedAt: evaluationPeriods.closedAt,
    })
    .from(evaluationPeriods)
    .orderBy(desc(evaluationPeriods.openedAt));

  return Promise.all(periods.map(async (period) => {
    const [peerCount, mentorCount] = await Promise.all([
      database.db.select({ value: count() }).from(peerEvaluations).where(eq(peerEvaluations.periodId, period.id)),
      database.db.select({ value: count() }).from(mentorEvaluations).where(eq(mentorEvaluations.periodId, period.id)),
    ]);
    return {
      ...period,
      peerCount: Number(peerCount[0]?.value ?? 0),
      mentorCount: Number(mentorCount[0]?.value ?? 0),
    };
  }));
}

export async function listCriterionSummaries(database: Database): Promise<Array<{
  id: string;
  kind: 'Peer' | 'Mentor';
  name: string;
  active: boolean;
  minScore: number;
  maxScore: number;
}>> {
  return database.db
    .select({
      id: evaluationCriteria.id,
      kind: evaluationCriteria.kind,
      name: evaluationCriteria.name,
      active: evaluationCriteria.active,
      minScore: evaluationCriteria.minScore,
      maxScore: evaluationCriteria.maxScore,
    })
    .from(evaluationCriteria)
    .orderBy(desc(evaluationCriteria.active), asc(evaluationCriteria.kind), asc(evaluationCriteria.sortOrder));
}
