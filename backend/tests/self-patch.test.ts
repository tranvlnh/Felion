import { describe, expect, it } from 'vitest';
import { selfPatchBody } from '#app/api/routes/people.js';

describe('selfPatchBody', () => {
  it('accepts contact fields', () => {
    expect(selfPatchBody.parse({ phone: '0901 234 567', birthDate: '2004-09-05', personalEmail: null })).toEqual({
      phone: '0901 234 567',
      birthDate: '2004-09-05',
      personalEmail: null,
    });
  });

  it('rejects any other field', () => {
    for (const body of [{ isAdmin: true }, { clubEmail: 'x@gdg.vn' }, { fullName: 'X' }, { departmentId: crypto.randomUUID() }]) {
      expect(selfPatchBody.safeParse(body).success).toBe(false);
    }
  });
});
