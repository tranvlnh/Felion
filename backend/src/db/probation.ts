import { normalizeTeamName } from '#app/domain/member.js';
import { splitRound } from '#app/domain/probation.js';
import type { Prisma } from '#app/generated/prisma/client.js';
import { AppError } from '#app/errors.js';
import { writeAudit } from './audit.js';
import { rethrowUniqueViolation, type Db, type Tx } from './client.js';
import { getSettings } from './settings.js';

export type TeamInput = { name: string; discordRoleId?: string | null | undefined };
export type TeamPatch = { name?: string | undefined; discordRoleId?: string | null | undefined };

const uniqueMessage = 'Another team already uses this name or Discord role.';

const personBrief = {
  id: true,
  fullName: true,
  studentId: true,
  status: true,
  discordUserId: true,
  isAdmin: true,
  probationApproved: true,
  roles: { select: { discordRoleId: true }, orderBy: { discordRoleId: 'asc' } },
} satisfies Prisma.PersonSelect;

const brief = ({ roles, ...person }: Prisma.PersonGetPayload<{ select: typeof personBrief }>) =>
  ({ ...person, roles: roles.map((role) => role.discordRoleId) });

export async function getProbationOverview(db: Db) {
  const [teams, unassigned] = await Promise.all([
    db.probationTeam.findMany({
      orderBy: { name: 'asc' },
      select: {
        id: true,
        name: true,
        discordRoleId: true,
        candidates: { select: personBrief, orderBy: { fullName: 'asc' } },
        mentors: { select: { person: { select: personBrief } }, orderBy: { person: { fullName: 'asc' } } },
      },
    }),
    db.person.findMany({ where: { kind: 'Candidate', teamId: null }, select: personBrief, orderBy: { fullName: 'asc' } }),
  ]);
  return {
    teams: teams.map((team) => ({ ...team, candidates: team.candidates.map(brief), mentors: team.mentors.map((mentor) => brief(mentor.person)) })),
    unassigned: unassigned.map(brief),
  };
}

export async function getTeam(db: Db | Tx, id: string) {
  const team = await db.probationTeam.findUnique({ where: { id }, select: { id: true, name: true, discordRoleId: true } });
  if (!team) {
    throw new AppError('NOT_FOUND', 'Team not found.');
  }
  return team;
}

// Non-admins never act on admins, so a team with an admin in it counts as an admin target.
export async function teamHasAdmin(db: Db, teamId: string): Promise<boolean> {
  return (await db.person.count({ where: { isAdmin: true, OR: [{ teamId }, { mentoring: { some: { teamId } } }] } })) > 0;
}

async function teamPeopleIds(tx: Tx, teamId: string): Promise<string[]> {
  const candidates = await tx.person.findMany({ where: { teamId }, select: { id: true } });
  const mentors = await tx.probationTeamMentor.findMany({ where: { teamId }, select: { personId: true } });
  return [...candidates.map((row) => row.id), ...mentors.map((row) => row.personId)];
}

// Team roles are unique per team, so dropping one never touches a role the person holds for another reason.
async function swapRole(tx: Tx, personIds: readonly string[], from: string | null, to: string | null): Promise<void> {
  if (from === to || personIds.length === 0) return;
  if (from) {
    await tx.personRole.deleteMany({ where: { personId: { in: [...personIds] }, discordRoleId: from } });
  }
  if (to) {
    await tx.personRole.createMany({ data: personIds.map((personId) => ({ personId, discordRoleId: to })), skipDuplicates: true });
  }
}

export async function createTeam(db: Db, input: TeamInput, actorId: string) {
  const data = { name: normalizeTeamName(input.name), discordRoleId: input.discordRoleId ?? null };
  try {
    return await db.$transaction(async (tx) => {
      const team = await tx.probationTeam.create({ data, select: { id: true, name: true, discordRoleId: true } });
      await writeAudit(tx, { actorId, action: 'ProbationTeamCreated', entityType: 'ProbationTeam', entityId: team.id, metadata: data });
      return team;
    });
  } catch (error) {
    rethrowUniqueViolation(error, uniqueMessage);
  }
}

// Returns the people whose Discord roles changed.
export async function updateTeam(db: Db, id: string, patch: TeamPatch, actorId: string): Promise<string[]> {
  const data: Prisma.ProbationTeamUpdateInput = {};
  if (patch.name !== undefined) data.name = normalizeTeamName(patch.name);
  if (patch.discordRoleId !== undefined) data.discordRoleId = patch.discordRoleId;
  try {
    return await db.$transaction(async (tx) => {
      const before = await getTeam(tx, id);
      await tx.probationTeam.update({ where: { id }, data });
      let changed: string[] = [];
      if (patch.discordRoleId !== undefined && patch.discordRoleId !== before.discordRoleId) {
        changed = await teamPeopleIds(tx, id);
        await swapRole(tx, changed, before.discordRoleId, patch.discordRoleId);
      }
      await writeAudit(tx, { actorId, action: 'ProbationTeamUpdated', entityType: 'ProbationTeam', entityId: id, metadata: { before, ...data } as Prisma.InputJsonValue });
      return changed;
    });
  } catch (error) {
    rethrowUniqueViolation(error, uniqueMessage);
  }
}

export async function deleteTeam(db: Db, id: string, actorId: string): Promise<string[]> {
  return db.$transaction(async (tx) => {
    const team = await getTeam(tx, id);
    const people = await teamPeopleIds(tx, id);
    await swapRole(tx, people, team.discordRoleId, null);
    await tx.probationTeam.delete({ where: { id } });
    await writeAudit(tx, { actorId, action: 'ProbationTeamDeleted', entityType: 'ProbationTeam', entityId: id, metadata: team });
    return people;
  });
}

export async function setCandidateTeam(db: Db, personId: string, teamId: string | null, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    const person = await tx.person.findUnique({ where: { id: personId }, select: { kind: true, team: { select: { id: true, discordRoleId: true } } } });
    if (!person) throw new AppError('NOT_FOUND', 'Person not found.');
    if (person.kind !== 'Candidate') throw new AppError('CONFLICT', 'Only probation candidates belong to a team.');
    const next = teamId ? await getTeam(tx, teamId) : null;
    await tx.person.update({ where: { id: personId }, data: { teamId } });
    await swapRole(tx, [personId], person.team?.discordRoleId ?? null, next?.discordRoleId ?? null);
    await writeAudit(tx, { actorId, action: 'CandidateTeamSet', entityType: 'Person', entityId: personId, metadata: { from: person.team?.id ?? null, to: teamId } });
  });
}

export async function addMentor(db: Db, teamId: string, personId: string, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    const team = await getTeam(tx, teamId);
    const person = await tx.person.findUnique({ where: { id: personId }, select: { kind: true, status: true } });
    if (person?.kind !== 'Member' || person.status !== 'Active') {
      throw new AppError('VALIDATION', 'Mentors must be active members.');
    }
    await tx.probationTeamMentor.createMany({ data: [{ teamId, personId }], skipDuplicates: true });
    await swapRole(tx, [personId], null, team.discordRoleId);
    await writeAudit(tx, { actorId, action: 'MentorAdded', entityType: 'ProbationTeam', entityId: teamId, metadata: { personId } });
  });
}

export async function removeMentor(db: Db, teamId: string, personId: string, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    const team = await getTeam(tx, teamId);
    await tx.probationTeamMentor.deleteMany({ where: { teamId, personId } });
    await swapRole(tx, [personId], team.discordRoleId, null);
    await writeAudit(tx, { actorId, action: 'MentorRemoved', entityType: 'ProbationTeam', entityId: teamId, metadata: { personId } });
  });
}

// Only a mark: roles and kind change when the round ends.
export async function setApproval(db: Db, personId: string, approved: boolean, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    const person = await tx.person.findUnique({ where: { id: personId }, select: { kind: true, status: true } });
    if (!person) throw new AppError('NOT_FOUND', 'Person not found.');
    if (person.kind !== 'Candidate' || person.status !== 'Active') {
      throw new AppError('CONFLICT', 'Only an active probation candidate can be approved.');
    }
    await tx.person.update({ where: { id: personId }, data: { probationApproved: approved } });
    await writeAudit(tx, { actorId, action: approved ? 'CandidateApproved' : 'CandidateUnapproved', entityType: 'Person', entityId: personId });
  });
}

// Approved candidates become Members; every team and every other candidate is deleted. The next round starts empty.
export async function endProbationRound(db: Db, actorId: string) {
  return db.$transaction(async (tx) => {
    const settings = await getSettings(tx);
    const teams = await tx.probationTeam.findMany({ select: { name: true, discordRoleId: true, mentors: { select: { personId: true } } } });
    const candidates = await tx.person.findMany({
      where: { kind: 'Candidate' },
      select: { id: true, fullName: true, studentId: true, discordUserId: true, isAdmin: true, probationApproved: true },
    });
    const { promote, remove } = splitRound(candidates, settings);

    const teamRoleIds = teams.flatMap((team) => (team.discordRoleId ? [team.discordRoleId] : []));
    const probationRoleIds = [...(settings.probationRoleId ? [settings.probationRoleId] : []), ...teamRoleIds];
    const mentorIds = [...new Set(teams.flatMap((team) => team.mentors.map((mentor) => mentor.personId)))];
    const promotedIds = promote.map((candidate) => candidate.id);

    await tx.personRole.deleteMany({ where: { personId: { in: mentorIds }, discordRoleId: { in: teamRoleIds } } });
    if (promotedIds.length > 0) {
      await tx.person.updateMany({ where: { id: { in: promotedIds } }, data: { kind: 'Member', teamId: null, probationApproved: false } });
      await tx.personRole.deleteMany({ where: { personId: { in: promotedIds }, discordRoleId: { in: probationRoleIds } } });
      await tx.personRole.createMany({
        data: promotedIds.map((personId) => ({ personId, discordRoleId: settings.memberRoleId as string })),
        skipDuplicates: true,
      });
    }
    await tx.probationTeam.deleteMany({});
    await tx.person.deleteMany({ where: { id: { in: remove.map((candidate) => candidate.id) } } });

    const brief = ({ id, fullName, studentId }: { id: string; fullName: string; studentId: string }) => ({ id, fullName, studentId });
    await writeAudit(tx, {
      actorId,
      action: 'ProbationRoundEnded',
      entityType: 'ProbationRound',
      metadata: { teams: teams.map((team) => team.name), promoted: promote.map(brief), removed: remove.map(brief), ...settings },
    });
    return { promotedIds, removed: remove, mentorIds, strippedRoleIds: probationRoleIds };
  });
}
