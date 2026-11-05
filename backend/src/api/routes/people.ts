import type { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { createPerson, deletePerson, getPerson, listPeople, updatePerson } from '#app/db/people.js';
import { addPersonRole, getRoleWebPermissions, removePersonRole } from '#app/db/roles.js';
import { getGuildRole, getAssignableGuildRole } from '#app/discord/guild-roles.js';
import { kickPerson } from '#app/discord/kick.js';
import { importFromDiscord, syncPerson, type SyncOutcome } from '#app/discord/role-sync.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';
import { assertAdmin, assertCan, assertCanActOn, assertCanAssignRole, type Actor } from '../permissions.js';
import type { WebContext } from '../server.js';

const kind = z.enum(['Candidate', 'Member']);
const status = z.enum(['Active', 'Inactive']);
const discordId = z.string().regex(/^\d{17,20}$/, 'must be a Discord id');

export const listQuery = z.object({
  kind: kind.optional(),
  status: status.optional(),
  departmentId: z.string().uuid().optional(),
  generation: z.coerce.number().int().min(1).max(999).optional(),
  search: z.string().max(100).optional(),
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(1).max(100).default(50),
});

const patchBody = z.object({
  studentId: z.string(),
  fullName: z.string(),
  departmentId: z.string().uuid(),
  generation: z.number().int().min(1).max(999),
  status,
  clubEmail: z.string().nullable(),
  phone: z.string().trim().regex(/^\+?[0-9 .-]{8,20}$/, 'must be a phone number').nullable(),
  personalEmail: z.string().trim().toLowerCase().pipe(z.email().max(320)).nullable(),
  birthDate: z.iso.date().nullable(),
  facebookUrl: z.string().trim().pipe(z.url({ protocol: /^https?$/ }).max(500)).nullable(),
  discordUserId: discordId.nullable(),
  isAdmin: z.boolean(),
}).partial().strict();

// The only fields people may change on their own profile.
export const selfPatchBody = patchBody.pick({ phone: true, personalEmail: true, birthDate: true, facebookUrl: true }).strict();

export const createBody = patchBody.extend({
  kind,
  studentId: z.string(),
  fullName: z.string(),
  departmentId: z.string().uuid(),
  generation: z.number().int().min(1).max(999),
}).strict();

const idParams = z.object({ id: z.string().uuid() });
const roleParams = idParams.extend({ roleId: discordId });

const adminOnlyFields = ['clubEmail', 'discordUserId', 'isAdmin'] as const;

export function assertAdminForFields(actor: Actor, body: Record<string, unknown>): void {
  if (adminOnlyFields.some((field) => body[field] !== undefined)) {
    assertAdmin(actor);
  }
}

// Writes are already saved; a Discord failure is reported, not thrown, so the UI can offer a retry.
export async function trySync(context: WebContext, personId: string, actorId: string) {
  try {
    const sync: SyncOutcome = await syncPerson(context.db, await context.getGuild(), personId, actorId);
    return { sync, syncError: null };
  } catch (error) {
    return { sync: null, syncError: getPublicErrorMessage(error, 'Discord sync failed.') };
  }
}

export async function loadRoleGrants(context: WebContext, roleId: string) {
  const role = await getAssignableGuildRole(await context.getGuild(), roleId);
  return { webPermissions: await getRoleWebPermissions(context.db, roleId), discordPermissions: role.permissions.bitfield };
}

export function registerPeopleRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;

  app.get('/people', async (request) => {
    assertCan(request.actor, 'ViewPeople');
    return listPeople(db, listQuery.parse(request.query));
  });

  app.get('/people/:id', async (request) => {
    const { id } = idParams.parse(request.params);
    if (id !== request.actor.id) assertCan(request.actor, 'ViewPeople');
    return getPerson(db, id);
  });

  app.get('/people/:id/sync-preview', async (request) => {
    const { id } = idParams.parse(request.params);
    if (id !== request.actor.id) assertCan(request.actor, 'ViewPeople');
    return syncPerson(db, await context.getGuild(), id, request.actor.id, { dryRun: true });
  });

  app.post('/people', async (request, reply) => {
    const body = createBody.parse(request.body);
    assertCan(request.actor, 'ManagePeople');
    assertAdminForFields(request.actor, body);
    const person = await createPerson(db, body, request.actor.id);
    return reply.code(201).send({ person, ...(person.discordUserId ? await trySync(context, person.id, request.actor.id) : {}) });
  });

  app.patch('/me', async (request) => ({
    person: await updatePerson(db, request.actor.id, selfPatchBody.parse(request.body), request.actor.id),
  }));

  app.patch('/people/:id', async (request) => {
    const { id } = idParams.parse(request.params);
    const body = patchBody.parse(request.body);
    const before = await getPerson(db, id);
    assertCanActOn(request.actor, 'ManagePeople', before);
    assertAdminForFields(request.actor, body);

    const person = await updatePerson(db, id, body, request.actor.id);
    if (person.discordUserId && person.discordUserId !== before.discordUserId) {
      const guild = await context.getGuild();
      await importFromDiscord(db, guild, id, request.actor.id).catch(() => undefined);
      return { person, ...await trySync(context, id, request.actor.id) };
    }
    return { person };
  });

  app.post('/people/:id/roles', async (request) => {
    const { id } = idParams.parse(request.params);
    const { roleId } = z.object({ roleId: discordId }).strict().parse(request.body);
    assertCanAssignRole(request.actor, await getPerson(db, id), await loadRoleGrants(context, roleId));
    await addPersonRole(db, id, roleId, request.actor.id);
    return { person: await getPerson(db, id), ...await trySync(context, id, request.actor.id) };
  });

  app.delete('/people/:id/roles/:roleId', async (request) => {
    const { id, roleId } = roleParams.parse(request.params);
    const target = await getPerson(db, id);
    const role = await getGuildRole(await context.getGuild(), roleId).catch((error: unknown) => {
      if (error instanceof AppError && error.code === 'NOT_FOUND') return null;
      throw error;
    });
    if (role) {
      assertCanAssignRole(request.actor, target, {
        webPermissions: await getRoleWebPermissions(db, roleId),
        discordPermissions: role.permissions.bitfield,
      });
    } else {
      // The role is gone from Discord; removing the stale row grants nothing.
      assertCanActOn(request.actor, 'AssignRoles', target);
    }
    await removePersonRole(db, id, roleId, request.actor.id);
    return { person: await getPerson(db, id), ...await trySync(context, id, request.actor.id) };
  });

  app.post('/people/:id/sync', async (request) => {
    const { id } = idParams.parse(request.params);
    assertCanActOn(request.actor, 'AssignRoles', await getPerson(db, id));
    return syncPerson(db, await context.getGuild(), id, request.actor.id);
  });

  app.delete('/people/:id', async (request, reply) => {
    const { id } = idParams.parse(request.params);
    assertCanActOn(request.actor, 'ManagePeople', await getPerson(db, id));
    await deletePerson(db, id, request.actor.id);
    return reply.code(204).send();
  });

  app.post('/people/:id/kick', async (request) => {
    const { id } = idParams.parse(request.params);
    const { reason } = z.object({ reason: z.string().trim().min(1).max(400) }).strict().parse(request.body);
    assertCanActOn(request.actor, 'KickMembers', await getPerson(db, id));
    return kickPerson(db, await context.getGuild(), id, reason, request.actor.id);
  });
}
