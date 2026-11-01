import type { Guild, Role } from 'discord.js';
import { isAssignableRole } from '#app/domain/role-sync-plan.js';
import { AppError } from '#app/errors.js';

export type GuildRoleView = {
  id: string;
  name: string;
  color: number;
  position: number;
  permissions: string;
  managed: boolean;
  assignable: boolean;
  memberCount: number;
};

export type GuildRoleInput = {
  name?: string | undefined;
  color?: number | undefined;
  permissions?: bigint | undefined;
};

const reason = 'Felion web role management';

function toView(role: Role, guildId: string): GuildRoleView {
  return {
    id: role.id,
    name: role.name,
    color: role.colors.primaryColor,
    position: role.position,
    permissions: role.permissions.bitfield.toString(),
    managed: role.managed,
    assignable: isAssignableRole({ id: role.id, managed: role.managed, editable: role.editable }, guildId),
    // Only members in the cache are counted; the bot caches members it has fetched.
    memberCount: role.members.size,
  };
}

export async function listGuildRoles(guild: Guild): Promise<GuildRoleView[]> {
  const roles = await guild.roles.fetch();
  return roles
    .filter((role) => role.id !== guild.id)
    .map((role) => toView(role, guild.id))
    .sort((a, b) => b.position - a.position);
}

export async function getGuildRole(guild: Guild, roleId: string): Promise<Role> {
  const role = await guild.roles.fetch(roleId);
  if (!role || role.id === guild.id) {
    throw new AppError('NOT_FOUND', 'Discord role not found.');
  }
  return role;
}

export async function getAssignableGuildRole(guild: Guild, roleId: string): Promise<Role> {
  const role = await getGuildRole(guild, roleId);
  if (!isAssignableRole({ id: role.id, managed: role.managed, editable: role.editable }, guild.id)) {
    throw new AppError('VALIDATION', `The bot cannot manage the role ${role.name}. Move the bot's role above it in Discord.`);
  }
  return role;
}

export async function createGuildRole(guild: Guild, input: GuildRoleInput): Promise<GuildRoleView> {
  const role = await guild.roles.create({
    name: input.name ?? 'new role',
    ...(input.color === undefined ? {} : { colors: { primaryColor: input.color } }),
    ...(input.permissions === undefined ? { permissions: [] } : { permissions: input.permissions }),
    reason,
  });
  return toView(role, guild.id);
}

export async function editGuildRole(guild: Guild, roleId: string, input: GuildRoleInput): Promise<GuildRoleView> {
  const role = await getAssignableGuildRole(guild, roleId);
  const edited = await role.edit({
    ...(input.name === undefined ? {} : { name: input.name }),
    ...(input.color === undefined ? {} : { colors: { primaryColor: input.color } }),
    ...(input.permissions === undefined ? {} : { permissions: input.permissions }),
    reason,
  });
  return toView(edited, guild.id);
}

export async function deleteGuildRole(guild: Guild, roleId: string): Promise<void> {
  const role = await getAssignableGuildRole(guild, roleId);
  await role.delete(reason);
}
