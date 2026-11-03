import type { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { writeAudit } from '#app/db/audit.js';
import { deleteRoleEverywhere, getRolePermissionMap, setRoleWebPermissions } from '#app/db/roles.js';
import { getSettings, updateSettings } from '#app/db/settings.js';
import { createGuildRole, deleteGuildRole, editGuildRole, getAssignableGuildRole, listGuildRoles } from '#app/discord/guild-roles.js';
import { syncAll } from '#app/discord/role-sync.js';
import { listSendableChannels, publishVerificationMessage } from '#app/discord/verification.js';
import { assertAdmin } from '../permissions.js';
import type { WebContext } from '../server.js';

const discordId = z.string().regex(/^\d{17,20}$/, 'must be a Discord id');
const webPermission = z.enum(['ViewPeople', 'ManagePeople', 'AssignRoles', 'KickMembers', 'SendEmail']);

const roleBody = z.object({
  name: z.string().trim().min(1).max(100),
  color: z.number().int().min(0).max(0xffffff),
  permissions: z.string().regex(/^\d+$/).transform((value) => BigInt(value)),
  webPermissions: z.array(webPermission).max(10),
}).partial().strict();

const roleParams = z.object({ roleId: discordId });

export function registerRoleRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;

  app.get('/roles', async () => {
    const [roles, grants] = await Promise.all([listGuildRoles(await context.getGuild()), getRolePermissionMap(db)]);
    return roles.map((role) => ({ ...role, webPermissions: grants.get(role.id) ?? [] }));
  });

  app.post('/roles', async (request, reply) => {
    assertAdmin(request.actor);
    const { webPermissions, ...input } = roleBody.parse(request.body);
    const role = await createGuildRole(await context.getGuild(), input);
    if (webPermissions?.length) await setRoleWebPermissions(db, role.id, webPermissions, request.actor.id);
    await writeAudit(db, {
      actorId: request.actor.id,
      action: 'DiscordRoleCreated',
      entityType: 'DiscordRole',
      entityId: role.id,
      metadata: { name: role.name, permissions: role.permissions },
    });
    return reply.code(201).send({ ...role, webPermissions: webPermissions ?? [] });
  });

  app.patch('/roles/:roleId', async (request) => {
    assertAdmin(request.actor);
    const { roleId } = roleParams.parse(request.params);
    const { webPermissions, ...input } = roleBody.parse(request.body);
    const guild = await context.getGuild();
    const role = Object.keys(input).length > 0
      ? await editGuildRole(guild, roleId, input)
      : (await getAssignableGuildRole(guild, roleId), null);
    if (webPermissions) await setRoleWebPermissions(db, roleId, webPermissions, request.actor.id);
    if (role) {
      await writeAudit(db, {
        actorId: request.actor.id,
        action: 'DiscordRoleEdited',
        entityType: 'DiscordRole',
        entityId: roleId,
        metadata: { name: role.name, color: role.color, permissions: role.permissions },
      });
    }
    return { ok: true };
  });

  app.delete('/roles/:roleId', async (request) => {
    assertAdmin(request.actor);
    const { roleId } = roleParams.parse(request.params);
    await deleteGuildRole(await context.getGuild(), roleId);
    // Also done by the roleDelete listener; repeating it is harmless and covers a missed event.
    await deleteRoleEverywhere(db, roleId, request.actor.id);
    return { ok: true };
  });

  app.get('/settings', async () => getSettings(db));

  app.patch('/settings', async (request) => {
    assertAdmin(request.actor);
    const body = z.object({ probationRoleId: discordId.nullable(), memberRoleId: discordId.nullable() })
      .partial().strict().parse(request.body);
    const guild = await context.getGuild();
    for (const roleId of [body.probationRoleId, body.memberRoleId]) {
      if (roleId) await getAssignableGuildRole(guild, roleId);
    }
    return updateSettings(db, body, request.actor.id);
  });

  app.get('/channels', async (request) => {
    assertAdmin(request.actor);
    return listSendableChannels(await context.getGuild());
  });

  // Saves the message text and publishes it (editing the existing message when possible).
  app.post('/verification-message', async (request) => {
    assertAdmin(request.actor);
    const { channelId, ...text } = z.object({
      channelId: discordId,
      verificationTitle: z.string().trim().min(1).max(256),
      verificationDescription: z.string().trim().min(1).max(4096),
      verificationButtonLabel: z.string().trim().min(1).max(80),
    }).strict().parse(request.body);
    const published = await publishVerificationMessage(await context.getGuild(), { ...await getSettings(db), ...text }, channelId);
    return updateSettings(db, {
      ...text,
      verificationChannelId: published.channelId,
      verificationMessageId: published.messageId,
    }, request.actor.id);
  });

  app.post('/sync-all', async (request) => {
    assertAdmin(request.actor);
    const { dryRun } = z.object({ dryRun: z.boolean().default(true) }).strict().parse(request.body ?? {});
    return syncAll(db, await context.getGuild(), request.actor.id, dryRun);
  });
}
