import type { Guild } from 'discord.js';
import { planDiscordRoleReconciliation, type DiscordRoleReconciliationPlan } from '../domain/discord-roles.js';
import type { Database } from '../db/client.js';
import {
  loadDiscordRoleSyncTarget,
  recordDiscordRoleSync,
  type DiscordRoleSyncTarget,
} from '../db/role-synchronization.js';

export type AppliedDiscordRoleSync = DiscordRoleReconciliationPlan & {
  addedRoleIds: string[];
  removedRoleIds: string[];
};

class DiscordRoleSyncError extends Error {
  readonly addedRoleIds: string[];
  readonly removedRoleIds: string[];

  constructor(message: string, addedRoleIds: string[], removedRoleIds: string[], options?: ErrorOptions) {
    super(message, options);
    this.name = 'DiscordRoleSyncError';
    this.addedRoleIds = addedRoleIds;
    this.removedRoleIds = removedRoleIds;
  }
}

export async function applyDiscordRoleSync(
  guild: Guild,
  target: DiscordRoleSyncTarget,
): Promise<AppliedDiscordRoleSync> {
  const member = await guild.members.fetch(target.discordUserId);
  const plan = planDiscordRoleReconciliation([...member.roles.cache.keys()], target);
  const guildRoles = await guild.roles.fetch();

  for (const roleId of [...plan.rolesToAdd, ...plan.rolesToRemove]) {
    const role = guildRoles.get(roleId);
    if (!role) {
      throw new DiscordRoleSyncError(
        `Configured Discord role ${roleId} no longer exists.`,
        [],
        [],
      );
    }
    if (role.id === guild.id || role.managed || !role.editable) {
      throw new DiscordRoleSyncError(
        `Discord role ${role.name} cannot be managed by Felion.`,
        [],
        [],
      );
    }
  }

  const addedRoleIds: string[] = [];
  const removedRoleIds: string[] = [];

  try {
    if (plan.rolesToAdd.length > 0) {
      await member.roles.add(plan.rolesToAdd, 'Felion role synchronization');
      addedRoleIds.push(...plan.rolesToAdd);
    }
    if (plan.rolesToRemove.length > 0) {
      await member.roles.remove(plan.rolesToRemove, 'Felion role synchronization');
      removedRoleIds.push(...plan.rolesToRemove);
    }
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : 'Discord rejected the role synchronization.';
    throw new DiscordRoleSyncError(message, addedRoleIds, removedRoleIds, { cause: error });
  }

  return { ...plan, addedRoleIds, removedRoleIds };
}

export async function synchronizeDiscordRolesForUser(
  database: NonNullable<Database>,
  guild: Guild,
  discordUserId: string,
  actorDiscordUserId: string,
): Promise<AppliedDiscordRoleSync> {
  const target = await loadDiscordRoleSyncTarget(database, discordUserId);
  let result: AppliedDiscordRoleSync;

  try {
    result = await applyDiscordRoleSync(guild, target);
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : 'Unable to synchronize Discord roles.';
    await recordDiscordRoleSync(database, {
      target,
      actorDiscordUserId,
      succeeded: false,
      addedRoleIds: error instanceof DiscordRoleSyncError ? error.addedRoleIds : [],
      removedRoleIds: error instanceof DiscordRoleSyncError ? error.removedRoleIds : [],
      error: message,
    });
    throw error;
  }

  await recordDiscordRoleSync(database, {
    target,
    actorDiscordUserId,
    succeeded: true,
    addedRoleIds: result.addedRoleIds,
    removedRoleIds: result.removedRoleIds,
  });
  return result;
}
