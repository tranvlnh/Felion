import type { FastifyInstance } from 'fastify';
import { z } from 'zod';
import {
  addMentor,
  createTeam,
  deleteTeam,
  endProbationRound,
  getProbationOverview,
  getTeam,
  removeMentor,
  setApproval,
  setCandidateTeam,
  teamHasAdmin,
  updateTeam,
} from '#app/db/probation.js';
import { getPerson } from '#app/db/people.js';
import { getRoleWebPermissions } from '#app/db/roles.js';
import { getGuildRole } from '#app/discord/guild-roles.js';
import { fetchGuildRoleInfo, stripRoles } from '#app/discord/role-sync.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';
import { assertAdmin, assertCan, assertCanActOn, assertCanAssignRole, type Actor } from '../permissions.js';
import type { WebContext } from '../server.js';
import { loadRoleGrants, trySync } from './people.js';

const discordId = z.string().regex(/^\d{17,20}$/, 'must be a Discord id');
const teamBody = z.object({ name: z.string(), discordRoleId: discordId.nullable() }).partial().strict();
const idParams = z.object({ id: z.string().uuid() });
const mentorParams = idParams.extend({ personId: z.string().uuid() });

type SyncError = { fullName: string; error: string };

// Writes are already saved; Discord failures are reported per person so the UI can point at who to fix.
async function syncEach(context: WebContext, personIds: readonly string[], actorId: string): Promise<SyncError[]> {
  const errors: SyncError[] = [];
  for (const personId of new Set(personIds)) {
    const { syncError } = await trySync(context, personId, actorId);
    if (syncError) {
      const person = await context.db.person.findUnique({ where: { id: personId }, select: { fullName: true } });
      errors.push({ fullName: person?.fullName ?? personId, error: syncError });
    }
  }
  return errors;
}

// Joining or leaving a team adds or removes its Discord role, so it needs the same rights as assigning that role.
async function assertCanMoveTeamRoles(
  context: WebContext,
  actor: Actor,
  target: { isAdmin: boolean },
  roleIds: readonly (string | null)[],
): Promise<void> {
  assertCanActOn(actor, 'ManagePeople', target);
  for (const roleId of roleIds) {
    if (!roleId) continue;
    const role = await getGuildRole(await context.getGuild(), roleId).catch((error: unknown) => {
      if (error instanceof AppError && error.code === 'NOT_FOUND') return null;
      throw error;
    });
    // A role gone from Discord grants nothing.
    if (role) {
      assertCanAssignRole(actor, target, {
        webPermissions: await getRoleWebPermissions(context.db, roleId),
        discordPermissions: role.permissions.bitfield,
      });
    }
  }
}

export function registerProbationRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;

  app.get('/probation', async (request) => {
    assertCan(request.actor, 'ViewPeople');
    return getProbationOverview(db);
  });

  app.post('/probation/teams', async (request, reply) => {
    const body = teamBody.required({ name: true }).parse(request.body);
    assertCan(request.actor, 'ManagePeople');
    if (body.discordRoleId) {
      assertCanAssignRole(request.actor, { isAdmin: false }, await loadRoleGrants(context, body.discordRoleId));
    }
    return reply.code(201).send(await createTeam(db, body, request.actor.id));
  });

  app.patch('/probation/teams/:id', async (request) => {
    const { id } = idParams.parse(request.params);
    const body = teamBody.parse(request.body);
    const team = await getTeam(db, id);
    const target = { isAdmin: await teamHasAdmin(db, id) };
    if (body.discordRoleId !== undefined && body.discordRoleId !== team.discordRoleId) {
      if (body.discordRoleId) await loadRoleGrants(context, body.discordRoleId); // rejects roles the bot cannot manage
      await assertCanMoveTeamRoles(context, request.actor, target, [team.discordRoleId, body.discordRoleId]);
    } else {
      assertCan(request.actor, 'ManagePeople');
    }
    const changed = await updateTeam(db, id, body, request.actor.id);
    return { syncErrors: await syncEach(context, changed, request.actor.id) };
  });

  app.delete('/probation/teams/:id', async (request) => {
    const { id } = idParams.parse(request.params);
    const team = await getTeam(db, id);
    await assertCanMoveTeamRoles(context, request.actor, { isAdmin: await teamHasAdmin(db, id) }, [team.discordRoleId]);
    const changed = await deleteTeam(db, id, request.actor.id);
    return { syncErrors: await syncEach(context, team.discordRoleId ? changed : [], request.actor.id) };
  });

  app.put('/probation/candidates/:id', async (request) => {
    const { id } = idParams.parse(request.params);
    const { teamId } = z.object({ teamId: z.string().uuid().nullable() }).strict().parse(request.body);
    const person = await getPerson(db, id);
    const next = teamId ? await getTeam(db, teamId) : null;
    await assertCanMoveTeamRoles(context, request.actor, person, [person.team?.discordRoleId ?? null, next?.discordRoleId ?? null]);
    await setCandidateTeam(db, id, teamId, request.actor.id);
    return { syncErrors: await syncEach(context, [id], request.actor.id) };
  });

  app.put('/probation/teams/:id/mentors/:personId', async (request) => {
    const { id, personId } = mentorParams.parse(request.params);
    const team = await getTeam(db, id);
    await assertCanMoveTeamRoles(context, request.actor, await getPerson(db, personId), [team.discordRoleId]);
    await addMentor(db, id, personId, request.actor.id);
    return { syncErrors: await syncEach(context, [personId], request.actor.id) };
  });

  app.delete('/probation/teams/:id/mentors/:personId', async (request) => {
    const { id, personId } = mentorParams.parse(request.params);
    const team = await getTeam(db, id);
    await assertCanMoveTeamRoles(context, request.actor, await getPerson(db, personId), [team.discordRoleId]);
    await removeMentor(db, id, personId, request.actor.id);
    return { syncErrors: await syncEach(context, [personId], request.actor.id) };
  });

  // Only a mark; nothing reaches Discord until the round ends.
  app.put('/probation/candidates/:id/approval', async (request) => {
    const { id } = idParams.parse(request.params);
    const { approved } = z.object({ approved: z.boolean() }).strict().parse(request.body);
    assertCanActOn(request.actor, 'ManagePeople', await getPerson(db, id));
    await setApproval(db, id, approved, request.actor.id);
    return { syncErrors: [] };
  });

  app.post('/probation/end', async (request) => {
    assertAdmin(request.actor);
    const result = await endProbationRound(db, request.actor.id);

    // Deleted candidates can no longer be synced, so their Probation and team roles are removed directly.
    const syncErrors: SyncError[] = [];
    const linked = result.removed.filter((candidate) => candidate.discordUserId);
    if (linked.length > 0 && result.strippedRoleIds.length > 0) {
      try {
        const guild = await context.getGuild();
        const roles = await fetchGuildRoleInfo(guild);
        for (const candidate of linked) {
          await stripRoles(guild, candidate.discordUserId as string, result.strippedRoleIds, roles).catch((error: unknown) => {
            syncErrors.push({ fullName: candidate.fullName, error: getPublicErrorMessage(error, 'Discord rejected the role change.') });
          });
        }
      } catch (error) {
        syncErrors.push({ fullName: '—', error: getPublicErrorMessage(error, 'Discord is unavailable.') });
      }
    }
    syncErrors.push(...await syncEach(context, [...result.promotedIds, ...result.mentorIds], request.actor.id));
    return { promoted: result.promotedIds.length, deleted: result.removed.length, syncErrors };
  });
}
