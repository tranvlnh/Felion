import { and, eq } from 'drizzle-orm';
import { normalizeStudentId } from '#app/domain/member.js';
import type { Database } from './client.js';
import {
  auditLogs,
  discordIdentityLinks,
  identityRegistry,
  members,
  probationCandidates,
} from './schema.js';

export async function linkDiscordIdentity(
  database: Database,
  discordUserId: string,
  rawStudentId: string,
): Promise<void> {
  const studentId = normalizeStudentId(rawStudentId);

  await database.db.transaction(async (transaction) => {
    const existingUserLink = await transaction
      .select({ id: discordIdentityLinks.id })
      .from(discordIdentityLinks)
      .where(eq(discordIdentityLinks.discordUserId, discordUserId))
      .limit(1);

    if (existingUserLink[0]) {
      throw new Error('This Discord account is already linked.');
    }

    const identity = await transaction
      .select({ subjectType: identityRegistry.subjectType, subjectId: identityRegistry.subjectId })
      .from(identityRegistry)
      .where(eq(identityRegistry.studentId, studentId))
      .limit(1);

    if (!identity[0]) {
      throw new Error('No active Member or ProbationCandidate matches this StudentId.');
    }

    const activeSubject = identity[0].subjectType === 'Member'
      ? (await transaction
        .select({ id: members.id })
        .from(members)
        .where(and(eq(members.id, identity[0].subjectId), eq(members.status, 'Active')))
        .limit(1))[0]
      : (await transaction
        .select({ id: probationCandidates.id })
        .from(probationCandidates)
        .where(and(
          eq(probationCandidates.id, identity[0].subjectId),
          eq(probationCandidates.status, 'Active'),
        ))
        .limit(1))[0];

    if (!activeSubject) {
      throw new Error('No active Member or ProbationCandidate matches this StudentId.');
    }

    const existingSubjectLink = await transaction
      .select({ id: discordIdentityLinks.id })
      .from(discordIdentityLinks)
      .where(eq(discordIdentityLinks.subjectId, identity[0].subjectId))
      .limit(1);

    if (existingSubjectLink[0]) {
      throw new Error('This identity is already linked to another Discord account.');
    }

    await transaction.insert(discordIdentityLinks).values({
      discordUserId,
      subjectType: identity[0].subjectType,
      subjectId: identity[0].subjectId,
    });

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: discordUserId,
      action: 'DiscordIdentityLinked',
      entityType: identity[0].subjectType,
      entityId: identity[0].subjectId,
      metadata: { studentId },
    });
  });
}
