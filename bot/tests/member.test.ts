import { describe, expect, it } from 'vitest';
import { createMember, normalizeClubEmail, normalizeStudentId } from '../src/domain/member.js';

describe('member domain', () => {
  it('normalizes StudentId and club email', () => {
    expect(normalizeStudentId(' sv-001 ')).toBe('SV-001');
    expect(normalizeClubEmail(' User@Example.COM ')).toBe('user@example.com');
  });

  it('creates an active member with normalized identity fields', () => {
    const member = createMember({
      studentId: 'sv-001',
      fullName: '  Example   Member ',
      clubEmail: 'Member@Example.com',
      departmentId: 'department-id',
      generationId: 'generation-id',
      position: 'Member',
    });

    expect(member).toMatchObject({
      studentId: 'SV-001',
      fullName: 'Example Member',
      clubEmail: 'member@example.com',
      active: true,
    });
  });

  it('rejects malformed identity fields', () => {
    expect(() => normalizeStudentId('')).toThrow();
    expect(() => normalizeClubEmail('invalid')).toThrow();
  });
});
