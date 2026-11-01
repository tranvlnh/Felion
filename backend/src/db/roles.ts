import type { WebPermission } from '#app/generated/prisma/enums.js';
import type { Actor } from '#app/api/permissions.js';
import { writeAudit } from './audit.js';
import type { Db, Tx } from './client.js';

export async function getPersonRoleIds(db: Db | Tx, personId: string): Promise<string[]> {
  const rows = await db.personRole.findMany({ where: { personId }, select: { discordRoleId: true } });
  return rows.map((row) => row.discordRoleId).sort();
}

export async function addPersonRole(db: Db, personId: string, discordRoleId: string, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    await tx.personRole.createMany({ data: [{ personId, discordRoleId }], skipDuplicates: true });
    await writeAudit(tx, { actorId, action: 'PersonRoleAdded', entityType: 'Person', entityId: personId, metadata: { discordRoleId } });
  });
}

export async function removePersonRole(db: Db, personId: string, discordRoleId: string, actorId: string): Promise<void> {
  await db.$transaction(async (tx) => {
    await tx.personRole.deleteMany({ where: { personId, discordRoleId } });
    await writeAudit(tx, { actorId, action: 'PersonRoleRemoved', entityType: 'Person', entityId: personId, metadata: { discordRoleId } });
  });
}

export async function mergePersonRoles(db: Db | Tx, personId: string, discordRoleIds: readonly string[]): Promise<number> {
  const result = await db.personRole.createMany({
    data: discordRoleIds.map((discordRoleId) => ({ personId, discordRoleId })),
    skipDuplicates: true,
  });
  return result.count;
}

export async function getRolePermissionMap(db: Db): Promise<Map<string, WebPermission[]>> {
  const rows = await db.rolePermission.findMany();
  return new Map(rows.map((row) => [row.discordRoleId, row.webPermissions]));
}

export async function getRoleWebPermissions(db: Db, discordRoleId: string): Promise<WebPermission[]> {
  const row = await db.rolePermission.findUnique({ where: { discordRoleId } });
  return row?.webPermissions ?? [];
}

export async function setRoleWebPermissions(
  db: Db,
  discordRoleId: string,
  webPermissions: readonly WebPermission[],
  actorId: string,
): Promise<void> {
  const unique = [...new Set(webPermissions)].sort();
  await db.$transaction(async (tx) => {
    if (unique.length === 0) {
      await tx.rolePermission.deleteMany({ where: { discordRoleId } });
    } else {
      await tx.rolePermission.upsert({
        where: { discordRoleId },
        create: { discordRoleId, webPermissions: unique },
        update: { webPermissions: unique },
      });
    }
    await writeAudit(tx, { actorId, action: 'RoleWebPermissionsSet', entityType: 'DiscordRole', entityId: discordRoleId, metadata: { webPermissions: unique } });
  });
}

export async function deleteRoleEverywhere(db: Db, discordRoleId: string, actorId: string | null): Promise<void> {
  await db.$transaction(async (tx) => {
    const removed = await tx.personRole.deleteMany({ where: { discordRoleId } });
    await tx.rolePermission.deleteMany({ where: { discordRoleId } });
    await tx.settings.updateMany({ where: { probationRoleId: discordRoleId }, data: { probationRoleId: null } });
    await tx.settings.updateMany({ where: { memberRoleId: discordRoleId }, data: { memberRoleId: null } });
    await tx.probationTeam.updateMany({ where: { discordRoleId }, data: { discordRoleId: null } });
    await writeAudit(tx, { actorId, action: 'DiscordRoleForgotten', entityType: 'DiscordRole', entityId: discordRoleId, metadata: { removedAssignments: removed.count } });
  });
}

// Reloaded on every request so deactivation or a lost club email revokes access immediately.
export async function loadActor(db: Db, personId: string): Promise<Actor | null> {
  const person = await db.person.findFirst({
    where: { id: personId, status: 'Active', clubEmail: { not: null } },
    select: { id: true, isAdmin: true, roles: { select: { discordRoleId: true } } },
  });
  if (!person) {
    return null;
  }

  const grants = await db.rolePermission.findMany({
    where: { discordRoleId: { in: person.roles.map((role) => role.discordRoleId) } },
  });
  return {
    id: person.id,
    isAdmin: person.isAdmin,
    permissions: new Set(grants.flatMap((grant) => grant.webPermissions)),
  };
}
