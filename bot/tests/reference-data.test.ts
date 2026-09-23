import { describe, expect, it } from 'vitest';
import {
  assertActiveReference,
  assertMutableDepartment,
  normalizeDepartmentReference,
  normalizeGenerationName,
  normalizeReferenceId,
} from '#app/domain/reference-data.js';

describe('reference data domain', () => {
  it('normalizes Department names and slugs', () => {
    expect(normalizeDepartmentReference({ name: '  Event   Department ', slug: ' EVENT-Team ' }))
      .toEqual({ name: 'Event Department', slug: 'event-team' });
  });

  it('rejects invalid and reserved Department values', () => {
    expect(() => normalizeDepartmentReference({ name: 'Event', slug: 'event_team' })).toThrow();
    expect(() => normalizeDepartmentReference({ name: 'Core', slug: 'core' })).toThrow('reserved');
  });

  it('normalizes Generation names without adding lifecycle timestamps', () => {
    expect(normalizeGenerationName('  Gen   12 ')).toBe('Gen 12');
  });

  it('validates stable reference IDs', () => {
    expect(normalizeReferenceId(' 550E8400-E29B-41D4-A716-446655440000 '))
      .toBe('550e8400-e29b-41d4-a716-446655440000');
    expect(() => normalizeReferenceId('event')).toThrow('invalid');
  });

  it('prevents changes to inactive references and the Core Department', () => {
    expect(() => assertActiveReference({ active: false })).toThrow('already inactive');
    expect(() => assertMutableDepartment({ slug: 'core', active: true })).toThrow('reserved');
    expect(() => assertMutableDepartment({ slug: 'event', active: false })).toThrow('already inactive');
  });
});
