import { AppError } from '#app/errors.js';

export function normalizeStudentId(value: string): string {
  const normalized = value.trim().toUpperCase();
  if (!/^[A-Z0-9][A-Z0-9._-]{1,31}$/.test(normalized)) {
    throw new AppError('VALIDATION', 'StudentId is invalid.');
  }
  return normalized;
}

export function normalizeClubEmail(value: string): string {
  const normalized = value.trim().toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalized)) {
    throw new AppError('VALIDATION', 'Club email is invalid.');
  }
  return normalized;
}

export function normalizeFullName(value: string): string {
  const normalized = value.trim().replace(/\s+/g, ' ');
  if (normalized.length < 2 || normalized.length > 200) {
    throw new AppError('VALIDATION', 'Full name must contain between 2 and 200 characters.');
  }
  return normalized;
}

export function normalizeTeamName(value: string): string {
  const normalized = value.trim().replace(/\s+/g, ' ');
  if (normalized.length < 1 || normalized.length > 100) {
    throw new AppError('VALIDATION', 'Team name must contain between 1 and 100 characters.');
  }
  return normalized;
}
