import { count, eq } from 'drizzle-orm';
import { createMember } from '../domain/member.js';
import { normalizeGenerationName } from '../domain/reference-data.js';
import type { Database } from './client.js';
import { auditLogs, departments, generations, identityRegistry, members } from './schema.js';

export type BootstrapAdminInput = {
  studentId: string;
  fullName: string;
  clubEmail: string;
  generationName: string;
  actorDiscordUserId: string;
};

export async function bootstrapAdmin(
  database: NonNullable<Database>,
  input: BootstrapAdminInput,
): Promise<void> {
  const generationName = normalizeGenerationName(input.generationName);

  await database.db.transaction(async (transaction) => {
    const existingMembers = await transaction.select({ value: count() }).from(members);
    if ((existingMembers[0]?.value ?? 0) > 0) {
      throw new Error('Bootstrap is allowed only when no Member exists.');
    }

    let coreDepartment = (await transaction
      .select()
      .from(departments)
      .where(eq(departments.slug, 'core'))
      .limit(1))[0];

    if (!coreDepartment) {
      coreDepartment = (await transaction
        .insert(departments)
        .values({ name: 'Core', slug: 'core' })
        .returning())[0];
    }
    if (coreDepartment && !coreDepartment.active) {
      throw new Error('The Core Department is inactive.');
    }

    let generation = (await transaction
      .select()
      .from(generations)
      .where(eq(generations.name, generationName))
      .limit(1))[0];

    if (!generation) {
      generation = (await transaction
        .insert(generations)
        .values({ name: generationName })
        .returning())[0];
    }
    if (generation && !generation.active) {
      throw new Error('The bootstrap Generation is inactive.');
    }

    if (!coreDepartment || !generation) {
      throw new Error('Unable to create bootstrap reference data.');
    }

    const member = createMember({
      studentId: input.studentId,
      fullName: input.fullName,
      clubEmail: input.clubEmail,
      departmentId: coreDepartment.id,
      generationId: generation.id,
      position: 'Admin',
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
      action: 'BootstrapAdminCreated',
      entityType: 'Member',
      entityId: member.id,
      metadata: { studentId: member.studentId },
    });
  });
}
