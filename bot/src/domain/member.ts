import { randomUUID } from 'node:crypto';

export const memberPositions = ['Admin', 'Core', 'Member'] as const;
export type MemberPosition = (typeof memberPositions)[number];

export type Member = {
  id: string;
  studentId: string;
  fullName: string;
  clubEmail: string;
  departmentId: string;
  generationId: string;
  position: MemberPosition;
  active: boolean;
};

export function normalizeStudentId(value: string): string {
  const normalized = value.trim().toUpperCase();
  if (!/^[A-Z0-9][A-Z0-9._-]{1,31}$/.test(normalized)) {
    throw new Error('StudentId is invalid.');
  }

  return normalized;
}

export function normalizeClubEmail(value: string): string {
  const normalized = value.trim().toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalized)) {
    throw new Error('Club email is invalid.');
  }

  return normalized;
}

export function normalizeFullName(value: string): string {
  const normalized = value.trim().replace(/\s+/g, ' ');
  if (normalized.length < 2 || normalized.length > 200) {
    throw new Error('Full name must contain between 2 and 200 characters.');
  }

  return normalized;
}

export function createMember(input: Omit<Member, 'id' | 'studentId' | 'clubEmail' | 'active'> & {
  studentId: string;
  clubEmail: string;
}): Member {
  if (!input.departmentId || !input.generationId) {
    throw new Error('Department and generation are required.');
  }

  return {
    id: randomUUID(),
    studentId: normalizeStudentId(input.studentId),
    fullName: normalizeFullName(input.fullName),
    clubEmail: normalizeClubEmail(input.clubEmail),
    departmentId: input.departmentId,
    generationId: input.generationId,
    position: input.position,
    active: true,
  };
}
