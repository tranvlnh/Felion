import type { Guild } from 'discord.js';
import { writeAudit } from '#app/db/audit.js';
import type { Db } from '#app/db/client.js';
import { AppError } from '#app/errors.js';
import { assertKeepsAnAdmin } from '#app/api/permissions.js';
import { fetchGuildMember } from './role-sync.js';

export async function kickPerson(
  db: Db,
  guild: Guild,
  personId: string,
  reason: string,
  actorId: string,
): Promise<{ kicked: boolean }> {
  const person = await db.person.findUnique({ where: { id: personId }, select: { discordUserId: true, isAdmin: true } });
  if (!person) {
    throw new AppError('NOT_FOUND', 'Person not found.');
  }
  if (person.isAdmin) {
    const otherActiveAdmins = await db.person.count({
      where: { isAdmin: true, status: 'Active', clubEmail: { not: null }, id: { not: personId } },
    });
    assertKeepsAnAdmin(otherActiveAdmins, person, true);
  }

  const member = person.discordUserId ? await fetchGuildMember(guild, person.discordUserId) : null;
  if (member) {
    if (!member.kickable) {
      throw new AppError('FORBIDDEN', 'The bot cannot kick this member (owner or higher role than the bot).');
    }
    await member.kick(reason);
  }

  // Inactive so a later rejoin does not get roles back and web access ends.
  await db.$transaction(async (tx) => {
    await tx.person.update({ where: { id: personId }, data: { status: 'Inactive' } });
    await writeAudit(tx, {
      actorId,
      action: 'PersonKicked',
      entityType: 'Person',
      entityId: personId,
      metadata: { reason, discordUserId: person.discordUserId, wasInServer: member !== null },
    });
  });
  return { kicked: member !== null };
}
