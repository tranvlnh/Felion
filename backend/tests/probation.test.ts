import { describe, expect, it } from 'vitest';
import { splitRound } from '#app/domain/probation.js';

const roles = { probationRoleId: 'p', memberRoleId: 'm' };
const person = (id: string, probationApproved: boolean, isAdmin = false) => ({ id, probationApproved, isAdmin });

describe('splitRound', () => {
  it('promotes approved candidates and removes the rest', () => {
    const { promote, remove } = splitRound([person('a', true), person('b', false), person('c', true)], roles);
    expect(promote.map((c) => c.id)).toEqual(['a', 'c']);
    expect(remove.map((c) => c.id)).toEqual(['b']);
  });

  it('needs the Probation and Member roles only when someone is promoted', () => {
    expect(() => splitRound([person('a', true)], { probationRoleId: 'p', memberRoleId: null })).toThrow(/Settings/);
    expect(splitRound([person('b', false)], { probationRoleId: null, memberRoleId: null }).remove).toHaveLength(1);
  });

  it('never deletes an admin', () => {
    expect(() => splitRound([person('a', false, true)], roles)).toThrow(/admin/);
    expect(splitRound([person('a', true, true)], roles).promote).toHaveLength(1);
  });
});
