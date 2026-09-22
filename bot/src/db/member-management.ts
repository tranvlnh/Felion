import { and, eq, or } from 'drizzle-orm';
import { createMember } from '../domain/member.js';
import type { Database } from './client.js';
import { auditLogs, departments, generations, identityRegistry, members } from './schema.js';

export type CreateMemberInput = {
  studentId: string;
  fullName: string;
  clubEmail: string;
  department: string;
  generation: string;
  actorDiscordUserId: string;
};

export async function createRegularMember(
  database: NonNullable<Database>,
  input: CreateMemberInput,
): Promise<string> {
  return database.db.transaction(async (transaction) => {
    const department = (await transaction
      .select()
      .from(departments)
      .where(or(eq(departments.slug, input.department.trim().toLowerCase()), eq(departments.name, input.department.trim())))
      .limit(1))[0];

    const generation = (await transaction
      .select()
      .from(generations)
      .where(eq(generations.name, input.generation.trim()))
      .limit(1))[0];

    if (!department || !generation) {
      throw new Error('Department or generation was not found.');
    }

    if (department.slug === 'core') {
      throw new Error('A regular Member cannot belong to the Core department.');
    }

    const member = createMember({
      studentId: input.studentId,
      fullName: input.fullName,
      clubEmail: input.clubEmail,
      departmentId: department.id,
      generationId: generation.id,
      position: 'Member',
    });

    await transaction.insert(members).values({
      id: member.id,
      studentId: member.studentId,
      fullName: member.fullName,
      clubEmail: member.clubEmail,
      departmentId: member.departmentId,
      generationId: member.generationId,
      position: member.position,
      status: 'Active',
    });

    await transaction.insert(identityRegistry).values({
      studentId: member.studentId,
      subjectType: 'Member',
      subjectId: member.id,
    });

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'MemberCreated',
      entityType: 'Member',
      entityId: member.id,
      metadata: { studentId: member.studentId },
    });

    return member.id;
  });
}
