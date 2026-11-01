import { PermissionFlagsBits } from 'discord.js';
import type { WebPermission } from '#app/generated/prisma/enums.js';
import { AppError } from '#app/errors.js';

export type Actor = {
  id: string;
  isAdmin: boolean;
  permissions: ReadonlySet<WebPermission>;
};

export type RoleGrants = {
  webPermissions: readonly WebPermission[];
  discordPermissions: bigint;
};

const dangerousDiscordPermissions = PermissionFlagsBits.Administrator
  | PermissionFlagsBits.ManageGuild
  | PermissionFlagsBits.ManageRoles
  | PermissionFlagsBits.BanMembers
  | PermissionFlagsBits.KickMembers
  | PermissionFlagsBits.ManageChannels
  | PermissionFlagsBits.ManageWebhooks;

export function can(actor: Actor, permission: WebPermission): boolean {
  return actor.isAdmin || actor.permissions.has(permission);
}

export function assertAdmin(actor: Actor): void {
  if (!actor.isAdmin) {
    throw new AppError('FORBIDDEN', 'Only admins can do this.');
  }
}

export function assertCan(actor: Actor, permission: WebPermission): void {
  if (!can(actor, permission)) {
    throw new AppError('FORBIDDEN', `Missing permission ${permission}.`);
  }
}

// Non-admins never act on admins, so delegated permissions cannot lock admins out.
export function assertCanActOn(actor: Actor, permission: WebPermission, target: { isAdmin: boolean }): void {
  assertCan(actor, permission);
  if (target.isAdmin && !actor.isAdmin) {
    throw new AppError('FORBIDDEN', 'Only admins can change another admin.');
  }
}

export function isPrivilegedRole(role: RoleGrants): boolean {
  return role.webPermissions.length > 0 || (role.discordPermissions & dangerousDiscordPermissions) !== 0n;
}

// Blocks self-escalation: a delegated AssignRoles cannot hand out roles that carry power.
export function assertCanAssignRole(actor: Actor, target: { isAdmin: boolean }, role: RoleGrants): void {
  assertCanActOn(actor, 'AssignRoles', target);
  if (!actor.isAdmin && isPrivilegedRole(role)) {
    throw new AppError('FORBIDDEN', 'Only admins can assign roles with web or moderation permissions.');
  }
}

export function assertKeepsAnAdmin(otherActiveAdmins: number, target: { isAdmin: boolean }, losesAdmin: boolean): void {
  if (target.isAdmin && losesAdmin && otherActiveAdmins === 0) {
    throw new AppError('CONFLICT', 'The last admin cannot lose admin access.');
  }
}
