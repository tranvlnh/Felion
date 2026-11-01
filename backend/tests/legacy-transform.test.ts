import { describe, expect, it } from 'vitest';
import { parseGenerationNumber, transformLegacy, type LegacyData } from '../scripts/legacy-transform.js';

const at = (day: number) => new Date(Date.UTC(2025, 0, day));

function sample(): LegacyData {
  return {
    departments: [{ id: 'dep', name: 'Tech', active: true, created_at: at(1) }],
    generations: [{ id: 'g3', name: 'Gen 3' }],
    members: [{
      id: 'm1', student_id: 'SV1', full_name: 'Mentor One', club_email: 'Mentor@Club.edu',
      department_id: 'dep', generation_id: 'g3', position: 'Admin', status: 'Active', created_at: at(1),
    }],
    candidates: [
      { id: 'c1', student_id: 'SV2', full_name: 'Cand One', department_id: 'dep', generation_id: 'g3', status: 'Active', created_at: at(2) },
      { id: 'c2', student_id: 'SV3', full_name: 'Cand Two', department_id: 'dep', generation_id: 'g3', status: 'Failed', created_at: at(2) },
    ],
    links: [
      { discord_user_id: '111', subject_type: 'Member', subject_id: 'm1' },
      { discord_user_id: '222', subject_type: 'ProbationCandidate', subject_id: 'c1' },
    ],
    mappings: [
      { kind: 'Position', key: 'Admin', discord_role_id: 'r-admin' },
      { kind: 'Position', key: 'Member', discord_role_id: 'r-member' },
      { kind: 'Probation', key: 'Active', discord_role_id: 'r-probation' },
      { kind: 'Department', key: 'dep', discord_role_id: 'r-tech' },
    ],
    assignments: [{ subject_type: 'ProbationCandidate', subject_id: 'c2', discord_role_id: 'r-extra' }],
  };
}

describe('parseGenerationNumber', () => {
  it('reads the trailing number', () => {
    expect(parseGenerationNumber('Gen 3')).toBe(3);
    expect(parseGenerationNumber('K12 ')).toBe(12);
    expect(parseGenerationNumber('Alpha')).toBeNull();
  });
});

describe('transformLegacy', () => {
  it('merges members and candidates into people', () => {
    const result = transformLegacy(sample());
    expect(result.problems).toEqual([]);
    const byId = new Map(result.people.map((person) => [person.id, person]));

    expect(byId.get('m1')).toMatchObject({
      kind: 'Member', status: 'Active', isAdmin: true, clubEmail: 'mentor@club.edu',
      discordUserId: '111', generation: 3,
    });
    expect(byId.get('c1')).toMatchObject({ kind: 'Candidate', status: 'Active', clubEmail: null, discordUserId: '222' });
    expect(byId.get('c2')).toMatchObject({ kind: 'Candidate', status: 'Inactive' });
  });

  it('reproduces the roles the old bot synced', () => {
    const { legacyRoles, settings } = transformLegacy(sample());
    expect(legacyRoles.get('m1')).toEqual(['r-admin', 'r-tech']);
    expect(legacyRoles.get('c1')).toEqual(['r-probation', 'r-tech']);
    expect(legacyRoles.get('c2')).toEqual([]);
    expect(settings).toEqual({ probationRoleId: 'r-probation', memberRoleId: 'r-member' });
  });

  it('stops on data it cannot convert', () => {
    const data = sample();
    data.generations.push({ id: 'gx', name: 'Alpha' });
    data.candidates.push({ ...data.candidates[0]!, id: 'c3', student_id: 'SV1' });
    data.departments.push({ ...data.departments[0]!, id: 'dep2' });
    expect(transformLegacy(data).problems).toHaveLength(3);
  });
});
