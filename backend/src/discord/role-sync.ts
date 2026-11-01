import { DiscordAPIError, RESTJSONErrorCodes, type Guild, type GuildMember } from 'discord.js';
import { writeAudit } from '#app/db/audit.js';
import type { Db } from '#app/db/client.js';
import { deleteRoleEverywhere, getPersonRoleIds, mergePersonRoles } from '#app/db/roles.js';
import { assignableRoleIds, planRoleSync, type GuildRoleInfo, type RoleSyncPlan } from '#app/domain/role-sync-plan.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';

export type SyncStatus = 'Synced' | 'Preview' | 'NotLinked' | 'Inactive' | 'NotInServer';
export type SyncOutcome = { status: SyncStatus; plan?: RoleSyncPlan };

const reason = 'Felion web role sync';

export async function fetchGuildMember(guild: Guild, discordUserId: string): Promise<GuildMember | null> {
  try {
    return await guild.members.fetch(discordUserId);
  } catch (error) {
    if (error instanceof DiscordAPIError && error.code === RESTJSONErrorCodes.UnknownMember) {
      return null;
    }
    throw error;
  }
}

export async function fetchGuildRoleInfo(guild: Guild): Promise<Map<string, GuildRoleInfo>> {
  const roles = await guild.roles.fetch();
  return new Map(roles.map((role) => [role.id, { id: role.id, managed: role.managed, editable: role.editable }]));
}

async function loadSyncSubject(db: Db, personId: string) {
  const person = await db.person.findUnique({
    where: { id: personId },
    select: { id: true, discordUserId: true, status: true },
  });
  if (!person) {
    throw new AppError('NOT_FOUND', 'Person not found.');
  }
  return person;
}

type SyncOptions = {
  dryRun?: boolean;
  roles?: Map<string, GuildRoleInfo>;
  member?: GuildMember | null;
};

export async function syncPerson(
  db: Db,
  guild: Guild,
  personId: string,
  actorId: string | null,
  options: SyncOptions = {},
): Promise<SyncOutcome> {
  const person = await loadSyncSubject(db, personId);
  if (!person.discordUserId) return { status: 'NotLinked' };
  if (person.status !== 'Active') return { status: 'Inactive' };

  const member = options.member !== undefined ? options.member : await fetchGuildMember(guild, person.discordUserId);
  if (!member) return { status: 'NotInServer' };

  const roles = options.roles ?? await fetchGuildRoleInfo(guild);
  const plan = planRoleSync([...member.roles.cache.keys()], await getPersonRoleIds(db, personId), roles, guild.id);
  if (options.dryRun) {
    return { status: 'Preview', plan };
  }

  for (const roleId of plan.missing) {
    await deleteRoleEverywhere(db, roleId, actorId);
  }

  try {
    if (plan.add.length > 0) await member.roles.add(plan.add, reason);
    if (plan.remove.length > 0) await member.roles.remove(plan.remove, reason);
  } catch (error) {
    const message = getPublicErrorMessage(error, 'Discord rejected the role change.');
    await writeAudit(db, {
      actorId,
      action: 'DiscordRoleSyncFailed',
      entityType: 'Person',
      entityId: personId,
      metadata: { add: plan.add, remove: plan.remove, error: message },
    });
    throw new AppError('EXTERNAL_SERVICE', `Saved, but Discord sync failed: ${message}`, { cause: error });
  }

  if (plan.add.length > 0 || plan.remove.length > 0 || plan.missing.length > 0) {
    await writeAudit(db, {
      actorId,
      action: 'DiscordRolesSynced',
      entityType: 'Person',
      entityId: personId,
      metadata: { add: plan.add, remove: plan.remove, missing: plan.missing },
    });
  }
  return { status: 'Synced', plan };
}

// For people already deleted from the database, where syncPerson no longer applies.
export async function stripRoles(
  guild: Guild,
  discordUserId: string,
  roleIds: readonly string[],
  roles: Map<string, GuildRoleInfo>,
): Promise<void> {
  const member = await fetchGuildMember(guild, discordUserId);
  if (!member) return;
  const remove = assignableRoleIds([...member.roles.cache.keys()], roles, guild.id).filter((roleId) => roleIds.includes(roleId));
  if (remove.length > 0) await member.roles.remove(remove, reason);
}

// Seeds the database from Discord so the first sync never strips roles people already have.
export async function importFromDiscord(
  db: Db,
  guild: Guild,
  personId: string,
  actorId: string | null,
  roles?: Map<string, GuildRoleInfo>,
): Promise<{ status: SyncStatus; imported: number }> {
  const person = await loadSyncSubject(db, personId);
  if (!person.discordUserId) return { status: 'NotLinked', imported: 0 };

  const member = await fetchGuildMember(guild, person.discordUserId);
  if (!member) return { status: 'NotInServer', imported: 0 };

  const roleIds = assignableRoleIds([...member.roles.cache.keys()], roles ?? await fetchGuildRoleInfo(guild), guild.id);
  const imported = await mergePersonRoles(db, personId, roleIds);
  if (imported > 0) {
    await writeAudit(db, { actorId, action: 'DiscordRolesImported', entityType: 'Person', entityId: personId, metadata: { roleIds } });
  }
  return { status: 'Synced', imported };
}

export type SyncAllReport = {
  checked: number;
  changed: { personId: string; fullName: string; add: string[]; remove: string[] }[];
  notInServer: number;
  failed: { personId: string; fullName: string; error: string }[];
};

export async function syncAll(db: Db, guild: Guild, actorId: string | null, dryRun: boolean): Promise<SyncAllReport> {
  const people = await db.person.findMany({
    where: { status: 'Active', discordUserId: { not: null } },
    select: { id: true, fullName: true, discordUserId: true },
    orderBy: { fullName: 'asc' },
  });
  // One bulk fetch instead of one request per person. With the GuildMembers intent a fully fetched cache stays
  // current, and Discord rate-limits repeated full fetches (opcode 8), so reuse it.
  const members = guild.members.cache.size >= guild.memberCount ? guild.members.cache : await guild.members.fetch();
  const roles = await fetchGuildRoleInfo(guild);
  const report: SyncAllReport = { checked: people.length, changed: [], notInServer: 0, failed: [] };

  for (const person of people) {
    try {
      const outcome = await syncPerson(db, guild, person.id, actorId, {
        dryRun,
        roles,
        member: members.get(person.discordUserId as string) ?? null,
      });
      if (outcome.status === 'NotInServer') {
        report.notInServer += 1;
      } else if (outcome.plan && (outcome.plan.add.length > 0 || outcome.plan.remove.length > 0)) {
        report.changed.push({ personId: person.id, fullName: person.fullName, add: outcome.plan.add, remove: outcome.plan.remove });
      }
    } catch (error) {
      report.failed.push({ personId: person.id, fullName: person.fullName, error: getPublicErrorMessage(error, 'Sync failed.') });
    }
  }
  return report;
}
