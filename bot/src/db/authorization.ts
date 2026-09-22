import { and, eq } from 'drizzle-orm';
import type { Database } from './client.js';
import { discordIdentityLinks, members } from './schema.js';

export async function isLinkedAdmin(
  database: NonNullable<Database>,
  discordUserId: string,
): Promise<boolean> {
  const result = await database.db
    .select({ memberId: members.id })
    .from(discordIdentityLinks)
    .innerJoin(members, eq(discordIdentityLinks.subjectId, members.id))
    .where(and(
      eq(discordIdentityLinks.discordUserId, discordUserId),
      eq(discordIdentityLinks.subjectType, 'Member'),
      eq(members.position, 'Admin'),
      eq(members.status, 'Active'),
    ))
    .limit(1);

  return result.length > 0;
}
