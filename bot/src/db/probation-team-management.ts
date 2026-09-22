import { and, eq } from 'drizzle-orm';
import { normalizeProbationTeamName } from '../domain/probation.js';
import { assertActiveReference, normalizeReferenceId } from '../domain/reference-data.js';
import type { Database } from './client.js';
import { auditLogs, probationTeams } from './schema.js';

export async function createProbationTeam(
  database: NonNullable<Database>,
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
  database: NonNullable<Database>,
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
  database: NonNullable<Database>,
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
