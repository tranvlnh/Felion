import { describe, expect, it } from 'vitest';
import { checkGoogleClaims, encodeSession, parseSession } from '#app/api/auth.js';

const domain = 'gdg.example.edu';

describe('checkGoogleClaims', () => {
  it('accepts a verified Workspace account and lowercases the email', () => {
    expect(checkGoogleClaims({ email: 'An.Nguyen@GDG.example.edu', email_verified: true, hd: 'gdg.example.edu' }, domain))
      .toBe('an.nguyen@gdg.example.edu');
  });

  it('rejects unverified, wrong-domain and personal accounts', () => {
    expect(checkGoogleClaims({ email: 'a@gdg.example.edu', email_verified: false, hd: domain }, domain)).toBeNull();
    expect(checkGoogleClaims({ email: 'a@other.edu', email_verified: true, hd: 'other.edu' }, domain)).toBeNull();
    expect(checkGoogleClaims({ email: 'a@gmail.com', email_verified: true }, domain)).toBeNull();
    expect(checkGoogleClaims({ email: 'a@other.edu', email_verified: true, hd: domain }, domain)).toBeNull();
    expect(checkGoogleClaims(undefined, domain)).toBeNull();
  });
});

describe('session value', () => {
  const personId = '550e8400-e29b-41d4-a716-446655440000';

  it('round-trips until it expires', () => {
    const value = encodeSession(personId, 1_000);
    expect(parseSession(value, 2_000)).toBe(personId);
    expect(parseSession(value, 1_000 + 8 * 24 * 60 * 60 * 1000)).toBeNull();
  });

  it('rejects malformed values', () => {
    expect(parseSession('not-a-session', 0)).toBeNull();
    expect(parseSession(`${personId}.abc`, 0)).toBeNull();
  });
});
