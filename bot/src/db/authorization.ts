import { and, eq } from 'drizzle-orm';
import type { MemberPosition } from '#app/domain/member.js';
import { AppError } from '#app/shared/errors/app-error.js';
import type { Database } from './client.js';
import {
  discordIdentityLinks,
  members,
  probationCandidates,
  probationTeams,
  teamMentors,
} from './schema.js';

export type ActiveMemberActor = {
  discordUserId: string;
  memberId: string;
  fullName: string;
  position: MemberPosition;
};

export type AdminActor = ActiveMemberActor & { position: 'Admin' };
export type CoreOrAdminActor = ActiveMemberActor & { position: 'Admin' | 'Core' };

export type CandidateActor = {
  discordUserId: string;
  candidateId: string;
  fullName: string;
  teamId: string | null;
};

export type MentorActor = ActiveMemberActor & {
  teamIds: string[];
};

async function findActiveMemberActor(
  database: Database,
  discordUserId: string,
): Promise<ActiveMemberActor | null> {
  const [actor] = await database.db
    .select({
      memberId: members.id,
      fullName: members.fullName,
      position: members.position,
    })
    .from(discordIdentityLinks)
    .innerJoin(members, eq(members.id, discordIdentityLinks.subjectId))
    .where(and(
      eq(discordIdentityLinks.discordUserId, discordUserId),
      eq(discordIdentityLinks.subjectType, 'Member'),
      eq(members.status, 'Active'),
    ))
    .limit(1);

  return actor ? { discordUserId, ...actor } : null;
}

export async function requireAdminActor(
  database: Database,
  discordUserId: string,
  publicMessage = 'Only a linked active Admin may perform this action.',
): Promise<AdminActor> {
  const actor = await findActiveMemberActor(database, discordUserId);
  if (!actor || actor.position !== 'Admin') {
    throw new AppError('FORBIDDEN', publicMessage);
  }

  return { ...actor, position: 'Admin' };
}

export async function requireCoreOrAdminActor(
  database: Database,
  discordUserId: string,
  publicMessage = 'Only a linked active Core or Admin may perform this action.',
): Promise<CoreOrAdminActor> {
  const actor = await findActiveMemberActor(database, discordUserId);
  if (!actor || (actor.position !== 'Admin' && actor.position !== 'Core')) {
    throw new AppError('FORBIDDEN', publicMessage);
  }

  return { ...actor, position: actor.position };
}

export async function requireCandidateActor(
  database: Database,
  discordUserId: string,
  publicMessage = 'Only a linked active ProbationCandidate may perform this action.',
): Promise<CandidateActor> {
  const [actor] = await database.db
    .select({
      candidateId: probationCandidates.id,
      fullName: probationCandidates.fullName,
      teamId: probationCandidates.teamId,
    })
    .from(discordIdentityLinks)
    .innerJoin(probationCandidates, eq(probationCandidates.id, discordIdentityLinks.subjectId))
    .where(and(
      eq(discordIdentityLinks.discordUserId, discordUserId),
      eq(discordIdentityLinks.subjectType, 'ProbationCandidate'),
      eq(probationCandidates.status, 'Active'),
    ))
    .limit(1);

  if (!actor) {
    throw new AppError('FORBIDDEN', publicMessage);
  }

  return { discordUserId, ...actor };
}

export async function requireMentorActor(
  database: Database,
  discordUserId: string,
  publicMessage = 'Only a linked active mentor may perform this action.',
): Promise<MentorActor> {
  const actor = await findActiveMemberActor(database, discordUserId);
  if (!actor) {
    throw new AppError('FORBIDDEN', publicMessage);
  }

  const teams = await database.db
    .select({ teamId: teamMentors.teamId })
    .from(teamMentors)
    .innerJoin(probationTeams, eq(probationTeams.id, teamMentors.teamId))
    .where(and(
      eq(teamMentors.memberId, actor.memberId),
      eq(probationTeams.active, true),
    ));

  if (teams.length === 0) {
    throw new AppError('FORBIDDEN', publicMessage);
  }

  return { ...actor, teamIds: teams.map(({ teamId }) => teamId) };
}
