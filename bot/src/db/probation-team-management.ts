import { and, eq } from 'drizzle-orm';
import { normalizeProbationTeamName } from '#app/domain/probation.js';
import { assertActiveReference, normalizeReferenceId } from '#app/domain/reference-data.js';
import type { Database } from './client.js';
import { auditLogs, members, probationTeams, teamMentors } from './schema.js';

export async function assignProbationTeamMentor(
  database: Database,
  input: { teamId: string; memberId: string; actorDiscordUserId: string },
): Promise<void> {
  const teamId = normalizeReferenceId(input.teamId);
  const memberId = normalizeReferenceId(input.memberId);

  await database.db.transaction(async (transaction) => {
    const team = (await transaction
      .select({ id: probationTeams.id })
      .from(probationTeams)
      .where(and(eq(probationTeams.id, teamId), eq(probationTeams.active, true)))
      .limit(1))[0];
    if (!team) {
      throw new Error('An active probation team was not found.');
    }

    const member = (await transaction
      .select({ id: members.id })
      .from(members)
      .where(and(eq(members.id, memberId), eq(members.status, 'Active')))
      .limit(1))[0];
    if (!member) {
      throw new Error('An active Member was not found.');
    }

    const assignment = (await transaction
      .insert(teamMentors)
      .values({ teamId, memberId })
      .onConflictDoNothing()
      .returning({ teamId: teamMentors.teamId }))[0];
    if (!assignment) {
      throw new Error('Member is already assigned as a mentor for this probation team.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationTeamMentorAssigned',
      entityType: 'ProbationTeam',
      entityId: teamId,
      metadata: { memberId },
    });
  });
}

export async function createProbationTeam(
  database: Database,
  input: { name: string; actorDiscordUserId: string },
): Promise<void> {
  const name = normalizeProbationTeamName(input.name);

  await database.db.transaction(async (transaction) => {
    const team = (await transaction
      .insert(probationTeams)
      .values({ name })
      .returning({ id: probationTeams.id }))[0];
    if (!team) {
      throw new Error('Unable to create probation team.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationTeamCreated',
      entityType: 'ProbationTeam',
      entityId: team.id,
      metadata: { name },
    });
  });
}

export async function editProbationTeam(
  database: Database,
  input: { teamId: string; name: string; actorDiscordUserId: string },
): Promise<void> {
  const teamId = normalizeReferenceId(input.teamId);
  const name = normalizeProbationTeamName(input.name);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(probationTeams)
      .where(eq(probationTeams.id, teamId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Probation team was not found.');
    }
    assertActiveReference(current);

    const updated = (await transaction
      .update(probationTeams)
      .set({ name, updatedAt: new Date() })
      .where(and(eq(probationTeams.id, teamId), eq(probationTeams.active, true)))
      .returning({ id: probationTeams.id }))[0];
    if (!updated) {
      throw new Error('Probation team is no longer active.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationTeamEdited',
      entityType: 'ProbationTeam',
      entityId: teamId,
      metadata: { previous: { name: current.name }, current: { name } },
    });
  });
}

export async function deactivateProbationTeam(
  database: Database,
  input: { teamId: string; actorDiscordUserId: string },
): Promise<void> {
  const teamId = normalizeReferenceId(input.teamId);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(probationTeams)
      .where(eq(probationTeams.id, teamId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Probation team was not found.');
    }
    assertActiveReference(current);

    const deactivated = (await transaction
      .update(probationTeams)
      .set({ active: false, updatedAt: new Date() })
      .where(and(eq(probationTeams.id, teamId), eq(probationTeams.active, true)))
      .returning({ id: probationTeams.id }))[0];
    if (!deactivated) {
      throw new Error('Probation team is already inactive.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationTeamDeactivated',
      entityType: 'ProbationTeam',
      entityId: teamId,
      metadata: { name: current.name },
    });
  });
}
