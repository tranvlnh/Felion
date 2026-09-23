import { describe, expect, it } from 'vitest';
import { AppError, getPublicErrorMessage } from '#app/shared/errors/app-error.js';

describe('application errors', () => {
  it('exposes the public message from AppError', () => {
    const error = new AppError('FORBIDDEN', 'Access denied.');

    expect(getPublicErrorMessage(error, 'Fallback.')).toBe('Access denied.');
  });

  it('keeps existing user-facing Error messages during incremental migration', () => {
    expect(getPublicErrorMessage(new Error('Validation failed.'), 'Fallback.'))
      .toBe('Validation failed.');
  });

  it('uses the fallback for unknown thrown values', () => {
    expect(getPublicErrorMessage({ reason: 'unknown' }, 'Fallback.')).toBe('Fallback.');
  });
});
