import { normalizeStudentId } from '#app/domain/member.js';
import { AppError } from '#app/errors.js';
import { writeAudit } from './audit.js';
import { rethrowUniqueViolation, type Db } from './client.js';

export type LinkResult = { result: 'Linked' | 'AlreadyLinked'; personId: string };

export async function linkDiscordIdentity(db: Db, discordUserId: string, rawStudentId: string): Promise<LinkResult> {
  const studentId = normalizeStudentId(rawStudentId);

  try {
    return await linkInTransaction(db, discordUserId, studentId);
  } catch (error) {
    rethrowUniqueViolation(error, 'This Discord account is already linked to another identity.');
  }
}

function linkInTransaction(db: Db, discordUserId: string, studentId: string): Promise<LinkResult> {
  return db.$transaction(async (tx) => {
    const person = await tx.person.findUnique({
      where: { studentId },
      select: { id: true, status: true, discordUserId: true },
    });
    if (!person || person.status !== 'Active') {
      throw new AppError('NOT_FOUND', 'No active Member or ProbationCandidate matches this StudentId.');
    }
    if (person.discordUserId === discordUserId) {
      return { result: 'AlreadyLinked', personId: person.id };
    }
    if (person.discordUserId) {
      throw new AppError('CONFLICT', 'This identity is already linked to another Discord account.');
    }

    const otherLink = await tx.person.findUnique({ where: { discordUserId }, select: { id: true } });
    if (otherLink) {
      throw new AppError('CONFLICT', 'This Discord account is already linked to another identity.');
    }

    await tx.person.update({ where: { id: person.id }, data: { discordUserId } });
    await writeAudit(tx, {
      actorId: person.id,
      action: 'DiscordIdentityLinked',
      entityType: 'Person',
      entityId: person.id,
      metadata: { studentId, discordUserId },
    });
    return { result: 'Linked', personId: person.id };
  });
}
