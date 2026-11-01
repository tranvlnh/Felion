export type GuildRoleInfo = {
  id: string;
  managed: boolean;
  editable: boolean;
};

export type RoleSyncPlan = {
  add: string[];
  remove: string[];
  // In the database but gone from Discord.
  missing: string[];
  // In the database but the bot cannot grant it (integration role or above the bot).
  unmanageable: string[];
};

export function isAssignableRole(role: GuildRoleInfo, guildId: string): boolean {
  return role.id !== guildId && !role.managed && role.editable;
}

export function planRoleSync(
  currentRoleIds: readonly string[],
  desiredRoleIds: readonly string[],
  guildRoles: ReadonlyMap<string, GuildRoleInfo>,
  guildId: string,
): RoleSyncPlan {
  const current = new Set(currentRoleIds);
  const desired = new Set(desiredRoleIds);
  const plan: RoleSyncPlan = { add: [], remove: [], missing: [], unmanageable: [] };

  for (const roleId of desired) {
    const role = guildRoles.get(roleId);
    if (!role) {
      plan.missing.push(roleId);
    } else if (!isAssignableRole(role, guildId)) {
      plan.unmanageable.push(roleId);
    } else if (!current.has(roleId)) {
      plan.add.push(roleId);
    }
  }

  for (const roleId of current) {
    const role = guildRoles.get(roleId);
    if (role && isAssignableRole(role, guildId) && !desired.has(roleId)) {
      plan.remove.push(roleId);
    }
  }

  for (const list of Object.values(plan)) {
    list.sort();
  }
  return plan;
}

export function assignableRoleIds(
  currentRoleIds: readonly string[],
  guildRoles: ReadonlyMap<string, GuildRoleInfo>,
  guildId: string,
): string[] {
  return currentRoleIds.filter((roleId) => {
    const role = guildRoles.get(roleId);
    return role !== undefined && isAssignableRole(role, guildId);
  });
}
