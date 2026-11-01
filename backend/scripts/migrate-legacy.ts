// One-off: copy the old Drizzle database into the new Prisma schema.
// Usage: npx tsx scripts/migrate-legacy.ts [--dry-run]
// Env: LEGACY_DATABASE_URL, DATABASE_URL, DISCORD_TOKEN, DISCORD_GUILD_ID
import { pathToFileURL } from 'node:url';
import { Client, GatewayIntentBits } from 'discord.js';
import postgres from 'postgres';
import { configSchema } from '../src/config.js';
import { createPrismaClient, type Db } from '../src/db/client.js';
import { fetchGuildRoleInfo, syncAll } from '../src/discord/role-sync.js';
import { assignableRoleIds, type GuildRoleInfo } from '../src/domain/role-sync-plan.js';
import { transformLegacy, type LegacyData, type TransformResult } from './legacy-transform.js';

type LegacyAuditRow = {
  id: string;
  actor_discord_user_id: string | null;
  action: string;
  entity_type: string;
  entity_id: string | null;
  metadata: unknown;
  created_at: Date;
};
export type LegacySnapshot = LegacyData & { auditLogs: LegacyAuditRow[] };

export type GuildSnapshot = {
  guildId: string;
  roles: Map<string, GuildRoleInfo>;
  memberRoles: Map<string, string[]>;
};

export type ImportPlan = {
  result: TransformResult;
  personRoles: Map<string, string[]>;
  stats: Record<string, number>;
};

export async function readLegacy(sql: postgres.Sql): Promise<LegacySnapshot> {
  return {
    departments: await sql`select id, name, active, created_at from departments`,
    generations: await sql`select id, name from generations`,
    members: await sql`select id, student_id, full_name, club_email, department_id, generation_id, position, status, created_at from members`,
    candidates: await sql`select id, student_id, full_name, department_id, generation_id, status, created_at from probation_candidates`,
    links: await sql`select discord_user_id, subject_type, subject_id from discord_identity_links`,
    mappings: await sql`select kind, key, discord_role_id from discord_role_mappings`,
    assignments: await sql`select subject_type, subject_id, discord_role_id from discord_role_assignments`,
    auditLogs: await sql`select id, actor_discord_user_id, action, entity_type, entity_id, metadata, created_at from audit_logs order by created_at`,
  } as unknown as LegacySnapshot;
}

// Live Discord roles win: seeding from the old mappings would make the first sync strip every
// role the old bot did not manage. People we cannot see in Discord fall back to the old set.
export function planImport(legacy: LegacySnapshot, guild: GuildSnapshot): ImportPlan {
  const result = transformLegacy(legacy);
  const personRoles = new Map<string, string[]>();
  const stats = { rolesFromDiscord: 0, rolesFromLegacyMappings: 0, discordDiffersFromOldMappings: 0 };

  for (const person of result.people) {
    if (person.status !== 'Active') continue;
    const legacyRoles = (result.legacyRoles.get(person.id) ?? []).filter((roleId) => guild.roles.has(roleId));
    const memberRoles = person.discordUserId ? guild.memberRoles.get(person.discordUserId) : undefined;
    if (memberRoles) {
      const live = assignableRoleIds(memberRoles, guild.roles, guild.guildId).sort();
      personRoles.set(person.id, live);
      stats.rolesFromDiscord += 1;
      if (live.join() !== legacyRoles.join()) stats.discordDiffersFromOldMappings += 1;
    } else {
      personRoles.set(person.id, legacyRoles);
      stats.rolesFromLegacyMappings += 1;
    }
  }

  return {
    result,
    personRoles,
    stats: {
      departments: legacy.departments.length,
      people: result.people.length,
      activePeople: personRoles.size,
      ...stats,
      personRoles: [...personRoles.values()].reduce((sum, list) => sum + list.length, 0),
      auditLogs: legacy.auditLogs.length,
    },
  };
}

export async function writeImport(prisma: Db, legacy: LegacySnapshot, plan: ImportPlan): Promise<void> {
  const { result, personRoles } = plan;
  const discordToPerson = new Map(result.people.flatMap((person) => person.discordUserId ? [[person.discordUserId, person.id] as const] : []));

  await prisma.$transaction(async (tx) => {
    for (const department of legacy.departments) {
      const data = { name: department.name, active: department.active, createdAt: department.created_at };
      await tx.department.upsert({ where: { id: department.id }, create: { id: department.id, ...data }, update: data });
    }
    for (const { id, ...data } of result.people) {
      await tx.person.upsert({ where: { id }, create: { id, ...data }, update: data });
    }
    await tx.personRole.deleteMany({ where: { personId: { in: result.people.map((person) => person.id) } } });
    await tx.personRole.createMany({
      data: [...personRoles].flatMap(([personId, roleIds]) => roleIds.map((discordRoleId) => ({ personId, discordRoleId }))),
      skipDuplicates: true,
    });
    await tx.settings.upsert({ where: { id: 1 }, create: { id: 1, ...result.settings }, update: result.settings });
    await tx.auditLog.createMany({
      data: legacy.auditLogs.map((row) => ({
        id: row.id,
        actorId: (row.actor_discord_user_id && discordToPerson.get(row.actor_discord_user_id)) ?? null,
        action: row.action,
        entityType: row.entity_type,
        entityId: row.entity_id,
        ...(row.metadata === null ? {} : { metadata: row.metadata as object }),
        createdAt: row.created_at,
      })),
      skipDuplicates: true,
    });
    await tx.auditLog.create({ data: { action: 'LegacyDataImported', entityType: 'System', metadata: plan.stats } });
  }, { timeout: 300_000 });
}

async function main(): Promise<void> {
  const env = configSchema
    .pick({ DATABASE_URL: true, DISCORD_TOKEN: true, DISCORD_GUILD_ID: true })
    .extend({ LEGACY_DATABASE_URL: configSchema.shape.DATABASE_URL })
    .parse(process.env);
  const dryRun = process.argv.includes('--dry-run');

  const sql = postgres(env.LEGACY_DATABASE_URL, { max: 2 });
  const legacy = await readLegacy(sql);
  await sql.end();

  const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMembers] });
  await client.login(env.DISCORD_TOKEN);
  try {
    const guild = await client.guilds.fetch(env.DISCORD_GUILD_ID);
    const members = await guild.members.fetch();
    const plan = planImport(legacy, {
      guildId: guild.id,
      roles: await fetchGuildRoleInfo(guild),
      memberRoles: new Map(members.map((member) => [member.id, [...member.roles.cache.keys()]])),
    });

    plan.result.warnings.forEach((warning) => console.warn(`WARN  ${warning}`));
    console.log({ ...plan.stats, settings: plan.result.settings });
    if (plan.result.problems.length > 0) {
      plan.result.problems.forEach((problem) => console.error(`ERROR ${problem}`));
      console.error('Fix these in the legacy database (or adjust the transform) and run again. Nothing was written.');
      process.exitCode = 1;
      return;
    }
    if (dryRun) {
      console.log('Dry run: nothing written.');
      return;
    }

    const prisma = createPrismaClient(env.DATABASE_URL);
    await writeImport(prisma, legacy, plan);
    const report = await syncAll(prisma, guild, null, true);
    console.log(`Sync preview after import: ${report.changed.length} people would change, ${report.failed.length} failed, ${report.notInServer} not in server.`);
    report.changed.slice(0, 20).forEach((change) => console.log(`  ${change.fullName}: +${change.add.join(',')} -${change.remove.join(',')}`));
    await prisma.$disconnect();
  } finally {
    await client.destroy();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error: unknown) => {
    console.error(error);
    process.exitCode = 1;
  });
}
