import { PermissionFlagsBits } from 'discord.js';
import { describe, expect, it } from 'vitest';
import type { WebPermission } from '#app/generated/prisma/enums.js';
import {
  assertCan,
  assertCanActOn,
  assertCanAssignRole,
  assertKeepsAnAdmin,
  type Actor,
} from '#app/api/permissions.js';

const actor = (permissions: WebPermission[], isAdmin = false): Actor => ({ id: 'a', isAdmin, permissions: new Set(permissions) });
const plainRole = { webPermissions: [], discordPermissions: PermissionFlagsBits.SendMessages };
const member = { isAdmin: false };
const admin = { isAdmin: true };

describe('web permissions', () => {
  it('lets admins do everything and others only what their roles grant', () => {
    expect(() => assertCan(actor([], true), 'KickMembers')).not.toThrow();
    expect(() => assertCan(actor(['ViewPeople']), 'ViewPeople')).not.toThrow();
    expect(() => assertCan(actor(['ViewPeople']), 'ManagePeople')).toThrow(/ManagePeople/);
  });

  it('stops non-admins from acting on admins', () => {
    expect(() => assertCanActOn(actor(['ManagePeople']), 'ManagePeople', member)).not.toThrow();
    expect(() => assertCanActOn(actor(['ManagePeople', 'KickMembers']), 'KickMembers', admin)).toThrow(/admin/);
    expect(() => assertCanActOn(actor([], true), 'KickMembers', admin)).not.toThrow();
  });

  it('lets AssignRoles hand out only roles without power', () => {
    const assigner = actor(['AssignRoles']);
    expect(() => assertCanAssignRole(assigner, member, plainRole)).not.toThrow();
    expect(() => assertCanAssignRole(assigner, member, { webPermissions: ['ViewPeople'], discordPermissions: 0n })).toThrow();
    expect(() => assertCanAssignRole(assigner, member, { webPermissions: [], discordPermissions: PermissionFlagsBits.Administrator })).toThrow();
    expect(() => assertCanAssignRole(assigner, member, { webPermissions: [], discordPermissions: PermissionFlagsBits.BanMembers | PermissionFlagsBits.SendMessages })).toThrow();
    expect(() => assertCanAssignRole(assigner, admin, plainRole)).toThrow();
    expect(() => assertCanAssignRole(actor([]), member, plainRole)).toThrow(/AssignRoles/);
    expect(() => assertCanAssignRole(actor([], true), member, { webPermissions: ['ManagePeople'], discordPermissions: PermissionFlagsBits.Administrator })).not.toThrow();
  });

  it('never removes the last admin', () => {
    expect(() => assertKeepsAnAdmin(0, admin, true)).toThrow(/last admin/);
    expect(() => assertKeepsAnAdmin(1, admin, true)).not.toThrow();
    expect(() => assertKeepsAnAdmin(0, admin, false)).not.toThrow();
    expect(() => assertKeepsAnAdmin(0, member, true)).not.toThrow();
  });
});
