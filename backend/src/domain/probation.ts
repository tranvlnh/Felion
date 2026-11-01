import { AppError } from '#app/errors.js';

export type RoundCandidate = { isAdmin: boolean; probationApproved: boolean };

// Approved candidates become Members when the round ends; everyone else is deleted.
export function splitRound<T extends RoundCandidate>(
  candidates: readonly T[],
  settings: { probationRoleId: string | null; memberRoleId: string | null },
): { promote: T[]; remove: T[] } {
  const promote = candidates.filter((candidate) => candidate.probationApproved);
  const remove = candidates.filter((candidate) => !candidate.probationApproved);
  if (promote.length > 0 && (!settings.probationRoleId || !settings.memberRoleId)) {
    throw new AppError('VALIDATION', 'Choose the Probation and Member roles in Settings before ending the round.');
  }
  if (remove.some((candidate) => candidate.isAdmin)) {
    throw new AppError('CONFLICT', 'An unapproved candidate is an admin; approve them or remove admin first.');
  }
  return { promote, remove };
}
