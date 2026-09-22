import { eq } from 'drizzle-orm';
import type { Database } from './client.js';
import { auditLogs, departments, discordRoleMappings, generations } from './schema.js';

export async function createDepartment(
  database: NonNullable<Database>,
  input: { name: string; slug: string; actorDiscordUserId: string },
): Promise<void> {
  const name = input.name.trim();
  const slug = input.slug.trim().toLowerCase();
  if (name.length < 2 || slug.length < 2 || !/^[a-z0-9-]+$/.test(slug)) {
    throw new Error('Department name or slug is invalid.');
  }
  if (slug === 'core') {
    throw new Error('The Core department is reserved.');
  }

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
  const name = input.name.trim();
  if (name.length < 2) {
    throw new Error('Generation name is invalid.');
  }

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

export async function mapDiscordRole(
  database: NonNullable<Database>,
  input: { kind: 'Position' | 'Probation' | 'Department' | 'Generation' | 'ProbationTeam'; key: string; discordRoleId: string; actorDiscordUserId: string },
): Promise<void> {
  const key = input.key.trim();
  if (!key || !/^\d+$/.test(input.discordRoleId)) {
    throw new Error('Role mapping key or Discord role ID is invalid.');
  }

  await database.db.transaction(async (transaction) => {
    const existing = (await transaction
      .select({ id: discordRoleMappings.id })
      .from(discordRoleMappings)
      .where(eq(discordRoleMappings.key, key))
      .limit(1))[0];

    let mappingId: string;
    if (existing) {
      const updated = (await transaction
        .update(discordRoleMappings)
        .set({ kind: input.kind, discordRoleId: input.discordRoleId, updatedAt: new Date() })
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
      metadata: { kind: input.kind, key, discordRoleId: input.discordRoleId },
    });
  });
}
