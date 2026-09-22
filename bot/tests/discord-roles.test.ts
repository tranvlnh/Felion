import { describe, expect, it } from 'vitest';
import {
  normalizeDiscordRoleMappingKey,
  parseDiscordRoleMappingKind,
  planDiscordRoleReconciliation,
  resolveDiscordRoleState,
  type DiscordRoleMapping,
} from '../src/domain/discord-roles.js';

const departmentId = '550e8400-e29b-41d4-a716-446655440000';
const generationId = '10b2a3c4-d5e6-4789-8abc-1234567890ab';
const teamId = '123e4567-e89b-42d3-a456-426614174000';

describe('Discord role mapping keys', () => {
  it('canonicalizes the approved stable mapping keys', () => {
    expect(normalizeDiscordRoleMappingKey('Position', 'Admin')).toBe('Admin');
    expect(normalizeDiscordRoleMappingKey('Probation', ' active ')).toBe('Active');
    expect(normalizeDiscordRoleMappingKey('Department', departmentId.toUpperCase())).toBe(departmentId);
  });

  it('rejects unknown kinds and mutable names used as reference keys', () => {
    expect(() => parseDiscordRoleMappingKind('Unknown')).toThrow('kind is invalid');
    expect(() => normalizeDiscordRoleMappingKey('Generation', 'Gen 12')).toThrow('UUID');
  });
});

describe('Discord role desired state', () => {
  const mappings: DiscordRoleMapping[] = [
    { kind: 'Position', key: 'Admin', discordRoleId: '100' },
    { kind: 'Position', key: 'Member', discordRoleId: '101' },
    { kind: 'Probation', key: 'Active', discordRoleId: '102' },
    { kind: 'Department', key: departmentId, discordRoleId: '103' },
    { kind: 'Generation', key: generationId, discordRoleId: '104' },
    { kind: 'ProbationTeam', key: teamId, discordRoleId: '105' },
    { kind: 'Generation', key: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', discordRoleId: '103' },
  ];

  it('resolves Member dimensions and preserves explicit assignments', () => {
    const state = resolveDiscordRoleState({
      subjectType: 'Member',
      position: 'Admin',
      departmentId,
      generationId,
    }, mappings, ['999']);

    expect(state.desiredRoleIds).toEqual(['100', '103', '104', '999']);
    expect(state.managedRoleIds).toEqual(['100', '101', '102', '103', '104', '105', '999']);
  });

  it('resolves active Probation, Department, Generation, and team roles', () => {
    const state = resolveDiscordRoleState({
      subjectType: 'ProbationCandidate',
      departmentId,
      generationId,
      teamId,
    }, mappings, []);

    expect(state.desiredRoleIds).toEqual(['102', '103', '104', '105']);
  });

  it('adds missing roles, removes obsolete managed roles, and preserves unrelated roles', () => {
    const plan = planDiscordRoleReconciliation(
      ['100', '101', '777'],
      {
        desiredRoleIds: ['100', '103'],
        managedRoleIds: ['100', '101', '102', '103'],
      },
    );

    expect(plan).toEqual({ rolesToAdd: ['103'], rolesToRemove: ['101'] });
  });

  it('is idempotent and keeps a shared role while any desired mapping needs it', () => {
    const state = resolveDiscordRoleState({
      subjectType: 'Member',
      position: 'Member',
      departmentId,
      generationId,
    }, [
      { kind: 'Position', key: 'Member', discordRoleId: '200' },
      { kind: 'Department', key: departmentId, discordRoleId: '200' },
    ], []);

    expect(planDiscordRoleReconciliation(['200'], state)).toEqual({
      rolesToAdd: [],
      rolesToRemove: [],
    });
  });
});
