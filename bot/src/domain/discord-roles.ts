import { normalizeReferenceId } from './reference-data.js';

export const discordRoleMappingKinds = [
  'Position',
  'Probation',
  'Department',
  'Generation',
  'ProbationTeam',
] as const;

export type DiscordRoleMappingKind = typeof discordRoleMappingKinds[number];

export type DiscordRoleMapping = {
  kind: DiscordRoleMappingKind;
  key: string;
  discordRoleId: string;
};

export type DiscordRoleSubject =
  | {
    subjectType: 'Member';
    position: 'Admin' | 'Core' | 'Member';
    departmentId: string;
    generationId: string;
    teamIds: readonly string[];
  }
  | {
    subjectType: 'ProbationCandidate';
    active: boolean;
    departmentId: string;
    generationId: string;
    teamId: string | null;
  };

export type DiscordRoleState = {
  desiredRoleIds: string[];
  managedRoleIds: string[];
};

export type DiscordRoleReconciliationPlan = {
  rolesToAdd: string[];
  rolesToRemove: string[];
};

export function isDiscordRoleMappingKind(value: string): value is DiscordRoleMappingKind {
  return discordRoleMappingKinds.some((kind) => kind === value);
}

export function parseDiscordRoleMappingKind(value: string): DiscordRoleMappingKind {
  if (!isDiscordRoleMappingKind(value)) {
    throw new Error('Discord role mapping kind is invalid.');
  }

  return value;
}

export function normalizeDiscordRoleMappingKey(kind: DiscordRoleMappingKind, value: string): string {
  const key = value.trim();

  if (kind === 'Position') {
    if (key !== 'Admin' && key !== 'Core' && key !== 'Member') {
      throw new Error('Position role mapping key must be Admin, Core, or Member.');
    }
    return key;
  }

  if (kind === 'Probation') {
    if (key.toLowerCase() !== 'active') {
      throw new Error('Probation role mapping key must be Active.');
    }
    return 'Active';
  }

  try {
    return normalizeReferenceId(key);
  } catch {
    throw new Error(`${kind} role mapping key must be a reference UUID.`);
  }
}

export function resolveDiscordRoleState(
  subject: DiscordRoleSubject,
  mappings: readonly DiscordRoleMapping[],
  explicitRoleIds: readonly string[],
): DiscordRoleState {
  const desiredMappingKeys = new Set<string>();

  if (subject.subjectType === 'Member') {
    desiredMappingKeys.add(mappingIdentity('Position', subject.position));
    desiredMappingKeys.add(mappingIdentity('Department', subject.departmentId));
    desiredMappingKeys.add(mappingIdentity('Generation', subject.generationId));
    for (const teamId of subject.teamIds) {
      desiredMappingKeys.add(mappingIdentity('ProbationTeam', teamId));
    }
  } else if (subject.active) {
    desiredMappingKeys.add(mappingIdentity('Probation', 'Active'));
    desiredMappingKeys.add(mappingIdentity('Department', subject.departmentId));
    desiredMappingKeys.add(mappingIdentity('Generation', subject.generationId));
    if (subject.teamId) {
      desiredMappingKeys.add(mappingIdentity('ProbationTeam', subject.teamId));
    }
  }

  const managedRoleIds = new Set(mappings.map((mapping) => mapping.discordRoleId));
  const desiredRoleIds = new Set(subject.subjectType === 'ProbationCandidate' && !subject.active
    ? []
    : explicitRoleIds);

  for (const mapping of mappings) {
    if (desiredMappingKeys.has(mappingIdentity(mapping.kind, mapping.key))) {
      desiredRoleIds.add(mapping.discordRoleId);
    }
  }

  for (const roleId of explicitRoleIds) {
    managedRoleIds.add(roleId);
  }

  return {
    desiredRoleIds: [...desiredRoleIds].sort(),
    managedRoleIds: [...managedRoleIds].sort(),
  };
}

export function planDiscordRoleReconciliation(
  currentRoleIds: readonly string[],
  roleState: DiscordRoleState,
): DiscordRoleReconciliationPlan {
  const current = new Set(currentRoleIds);
  const desired = new Set(roleState.desiredRoleIds);
  const managed = new Set(roleState.managedRoleIds);

  return {
    rolesToAdd: roleState.desiredRoleIds.filter((roleId) => !current.has(roleId)),
    rolesToRemove: currentRoleIds
      .filter((roleId) => managed.has(roleId) && !desired.has(roleId))
      .sort(),
  };
}

function mappingIdentity(kind: DiscordRoleMappingKind, key: string): string {
  return `${kind}\u0000${key}`;
}
