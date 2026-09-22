import { and, eq } from 'drizzle-orm';
import {
  assertActiveReference,
  assertMutableDepartment,
  normalizeDepartmentReference,
  normalizeGenerationName,
  normalizeReferenceId,
} from '../domain/reference-data.js';
import {
  normalizeDiscordRoleMappingKey,
  type DiscordRoleMappingKind,
} from '../domain/discord-roles.js';
import type { Database } from './client.js';
import { auditLogs, departments, discordRoleMappings, generations, probationTeams } from './schema.js';

export async function createDepartment(
  database: NonNullable<Database>,
  input: { name: string; slug: string; actorDiscordUserId: string },
): Promise<void> {
  const { name, slug } = normalizeDepartmentReference(input);

  await database.db.transaction(async (transaction) => {
    const department = (await transaction.insert(departments).values({ name, slug }).returning())[0];
    if (!department) {
      throw new Error('Unable to create department.');
    }
    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'DepartmentCreated',
      entityType: 'Department',
      entityId: department.id,
      metadata: { name, slug },
    });
  });
}

export async function createGeneration(
  database: NonNullable<Database>,
  input: { name: string; actorDiscordUserId: string },
): Promise<void> {
  const name = normalizeGenerationName(input.name);

  await database.db.transaction(async (transaction) => {
    const generation = (await transaction.insert(generations).values({ name }).returning())[0];
    if (!generation) {
      throw new Error('Unable to create generation.');
    }
    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'GenerationCreated',
      entityType: 'Generation',
      entityId: generation.id,
      metadata: { name },
    });
  });
}

export async function editDepartment(
  database: NonNullable<Database>,
  input: { departmentId: string; name: string; slug: string; actorDiscordUserId: string },
): Promise<void> {
  const departmentId = normalizeReferenceId(input.departmentId);
  const { name, slug } = normalizeDepartmentReference(input);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(departments)
      .where(eq(departments.id, departmentId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Department was not found.');
    }
    assertMutableDepartment(current);

    const updated = (await transaction
      .update(departments)
      .set({ name, slug, updatedAt: new Date() })
      .where(and(eq(departments.id, departmentId), eq(departments.active, true)))
      .returning({ id: departments.id }))[0];
    if (!updated) {
      throw new Error('Department is no longer active.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'DepartmentEdited',
      entityType: 'Department',
      entityId: departmentId,
      metadata: {
        previous: { name: current.name, slug: current.slug },
        current: { name, slug },
      },
    });
  });
}

export async function deactivateDepartment(
  database: NonNullable<Database>,
  input: { departmentId: string; actorDiscordUserId: string },
): Promise<void> {
  const departmentId = normalizeReferenceId(input.departmentId);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(departments)
      .where(eq(departments.id, departmentId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Department was not found.');
    }
    assertMutableDepartment(current);

    const deactivated = (await transaction
      .update(departments)
      .set({ active: false, updatedAt: new Date() })
      .where(and(eq(departments.id, departmentId), eq(departments.active, true)))
      .returning({ id: departments.id }))[0];
    if (!deactivated) {
      throw new Error('Department is already inactive.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'DepartmentDeactivated',
      entityType: 'Department',
      entityId: departmentId,
      metadata: { name: current.name, slug: current.slug },
    });
  });
}

export async function editGeneration(
  database: NonNullable<Database>,
  input: { generationId: string; name: string; actorDiscordUserId: string },
): Promise<void> {
  const generationId = normalizeReferenceId(input.generationId);
  const name = normalizeGenerationName(input.name);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(generations)
      .where(eq(generations.id, generationId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Generation was not found.');
    }
    assertActiveReference(current);

    const updated = (await transaction
      .update(generations)
      .set({ name })
      .where(and(eq(generations.id, generationId), eq(generations.active, true)))
      .returning({ id: generations.id }))[0];
    if (!updated) {
      throw new Error('Generation is no longer active.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'GenerationEdited',
      entityType: 'Generation',
      entityId: generationId,
      metadata: { previous: { name: current.name }, current: { name } },
    });
  });
}

export async function deactivateGeneration(
  database: NonNullable<Database>,
  input: { generationId: string; actorDiscordUserId: string },
): Promise<void> {
  const generationId = normalizeReferenceId(input.generationId);

  await database.db.transaction(async (transaction) => {
    const current = (await transaction
      .select()
      .from(generations)
      .where(eq(generations.id, generationId))
      .limit(1))[0];

    if (!current) {
      throw new Error('Generation was not found.');
    }
    assertActiveReference(current);

    const deactivated = (await transaction
      .update(generations)
      .set({ active: false })
      .where(and(eq(generations.id, generationId), eq(generations.active, true)))
      .returning({ id: generations.id }))[0];
    if (!deactivated) {
      throw new Error('Generation is already inactive.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'GenerationDeactivated',
      entityType: 'Generation',
      entityId: generationId,
      metadata: { name: current.name },
    });
  });
}

export async function mapDiscordRole(
  database: NonNullable<Database>,
  input: { kind: DiscordRoleMappingKind; key: string; discordRoleId: string; actorDiscordUserId: string },
): Promise<void> {
  const key = normalizeDiscordRoleMappingKey(input.kind, input.key);
  if (!/^\d+$/.test(input.discordRoleId)) {
    throw new Error('Discord role ID is invalid.');
  }

  await database.db.transaction(async (transaction) => {
    if (input.kind === 'Department') {
      const reference = await transaction
        .select({ id: departments.id })
        .from(departments)
        .where(eq(departments.id, key))
        .limit(1);
      if (!reference[0]) {
        throw new Error('The Department role mapping reference was not found.');
      }
    } else if (input.kind === 'Generation') {
      const reference = await transaction
        .select({ id: generations.id })
        .from(generations)
        .where(eq(generations.id, key))
        .limit(1);
      if (!reference[0]) {
        throw new Error('The Generation role mapping reference was not found.');
      }
    } else if (input.kind === 'ProbationTeam') {
      const reference = await transaction
        .select({ id: probationTeams.id })
        .from(probationTeams)
        .where(eq(probationTeams.id, key))
        .limit(1);
      if (!reference[0]) {
        throw new Error('The ProbationTeam role mapping reference was not found.');
      }
    }

    const existing = (await transaction
      .select({ id: discordRoleMappings.id, discordRoleId: discordRoleMappings.discordRoleId })
      .from(discordRoleMappings)
      .where(and(
        eq(discordRoleMappings.kind, input.kind),
        eq(discordRoleMappings.key, key),
      ))
      .limit(1))[0];

    let mappingId: string;
    if (existing) {
      const updated = (await transaction
        .update(discordRoleMappings)
        .set({ discordRoleId: input.discordRoleId, updatedAt: new Date() })
        .where(eq(discordRoleMappings.id, existing.id))
        .returning({ id: discordRoleMappings.id }))[0];
      if (!updated) {
        throw new Error('Unable to update role mapping.');
      }
      mappingId = updated.id;
    } else {
      const created = (await transaction
        .insert(discordRoleMappings)
        .values({ kind: input.kind, key, discordRoleId: input.discordRoleId })
        .returning({ id: discordRoleMappings.id }))[0];
      if (!created) {
        throw new Error('Unable to create role mapping.');
      }
      mappingId = created.id;
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: existing ? 'DiscordRoleMappingUpdated' : 'DiscordRoleMappingCreated',
      entityType: 'DiscordRoleMapping',
      entityId: mappingId,
      metadata: {
        kind: input.kind,
        key,
        discordRoleId: input.discordRoleId,
        ...(existing ? { previousDiscordRoleId: existing.discordRoleId } : {}),
      },
    });
  });
}
