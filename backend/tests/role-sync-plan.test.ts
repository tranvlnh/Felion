import { describe, expect, it } from 'vitest';
import { assignableRoleIds, planRoleSync, type GuildRoleInfo } from '#app/domain/role-sync-plan.js';

const guildId = 'guild';
const roles = new Map<string, GuildRoleInfo>([
  [guildId, { id: guildId, managed: false, editable: true }],
  ['member', { id: 'member', managed: false, editable: true }],
  ['event', { id: 'event', managed: false, editable: true }],
  ['booster', { id: 'booster', managed: true, editable: true }],
  ['owner', { id: 'owner', managed: false, editable: false }],
]);

describe('planRoleSync', () => {
  it('adds desired roles and removes assignable roles not in the database', () => {
    expect(planRoleSync([guildId, 'event'], ['member'], roles, guildId)).toEqual({
      add: ['member'],
      remove: ['event'],
      missing: [],
      unmanageable: [],
    });
  });

  it('never touches @everyone, integration roles or roles above the bot', () => {
    const plan = planRoleSync([guildId, 'booster', 'owner'], [], roles, guildId);
    expect(plan.remove).toEqual([]);
  });

  it('reports desired roles that were deleted or that the bot cannot grant', () => {
    const plan = planRoleSync([], ['gone', 'owner', 'booster'], roles, guildId);
    expect(plan).toEqual({ add: [], remove: [], missing: ['gone'], unmanageable: ['booster', 'owner'] });
  });

  it('is a no-op when Discord already matches', () => {
    expect(planRoleSync(['member', 'event'], ['event', 'member'], roles, guildId)).toEqual({ add: [], remove: [], missing: [], unmanageable: [] });
  });
});

describe('assignableRoleIds', () => {
  it('keeps only roles the bot can manage', () => {
    expect(assignableRoleIds([guildId, 'member', 'booster', 'owner', 'unknown'], roles, guildId)).toEqual(['member']);
  });
});
