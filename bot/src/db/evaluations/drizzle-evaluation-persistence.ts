import { and, asc, eq } from 'drizzle-orm';
import type {
  EvaluationCriterion,
  EvaluationScoreSnapshot,
} from '#app/domain/evaluation.js';
import type {
  EvaluationPersistence,
  EvaluationTransaction,
} from '#app/features/evaluations/application/contracts.js';
import type { Database, DbTransaction } from '#app/db/client.js';
import {
  auditLogs,
  discordIdentityLinks,
  evaluationCriteria,
  evaluationPeriods,
  members,
  mentorEvaluations,
  peerEvaluations,
  probationCandidates,
  probationTeams,
  teamMentors,
} from '#app/db/schema.js';

function toCriterion(row: typeof evaluationCriteria.$inferSelect): EvaluationCriterion {
  return {
    id: row.id,
    kind: row.kind,
    key: row.key,
    name: row.name,
    minScore: row.minScore,
    maxScore: row.maxScore,
    sortOrder: row.sortOrder,
    active: row.active,
  };
}

function toScoreSnapshots(value: unknown): EvaluationScoreSnapshot[] {
  if (!Array.isArray(value)) {
    throw new Error('Stored evaluation scores are invalid.');
  }

  return value.map((item) => {
    if (typeof item !== 'object' || item === null) {
      throw new Error('Stored evaluation scores are invalid.');
    }
    const snapshot = item as Record<string, unknown>;
    if (typeof snapshot.criterionId !== 'string'
      || typeof snapshot.criterionName !== 'string'
      || typeof snapshot.score !== 'number') {
      throw new Error('Stored evaluation scores are invalid.');
    }
    return {
      criterionId: snapshot.criterionId,
      criterionName: snapshot.criterionName,
      score: snapshot.score,
    };
  });
}

function createTransaction(transaction: DbTransaction): EvaluationTransaction {
  return {
    async listCriteria(kind) {
      const rows = await transaction
        .select()
        .from(evaluationCriteria)
        .where(eq(evaluationCriteria.kind, kind));
      return rows.map(toCriterion);
    },

    async listActiveCriteria(kind) {
      const rows = await transaction
        .select()
        .from(evaluationCriteria)
        .where(and(
          eq(evaluationCriteria.kind, kind),
          eq(evaluationCriteria.active, true),
        ))
        .orderBy(asc(evaluationCriteria.sortOrder), asc(evaluationCriteria.id));
      return rows.map(toCriterion);
    },

    async findCriterion(criterionId) {
      const [row] = await transaction
        .select()
        .from(evaluationCriteria)
        .where(eq(evaluationCriteria.id, criterionId))
        .limit(1);
      return row ? toCriterion(row) : null;
    },

    async createCriterion(input) {
      const [created] = await transaction
        .insert(evaluationCriteria)
        .values(input)
        .returning({ id: evaluationCriteria.id });
      return created?.id ?? null;
    },

    async reactivateCriterion(criterionId, name) {
      const [reactivated] = await transaction
        .update(evaluationCriteria)
        .set({ name, active: true, updatedAt: new Date() })
        .where(eq(evaluationCriteria.id, criterionId))
        .returning({ id: evaluationCriteria.id });
      return Boolean(reactivated);
    },

    async renameCriterion(criterionId, name) {
      await transaction
        .update(evaluationCriteria)
        .set({ name, updatedAt: new Date() })
        .where(eq(evaluationCriteria.id, criterionId));
    },

    async deactivateCriterion(criterionId) {
      await transaction
        .update(evaluationCriteria)
        .set({ active: false, updatedAt: new Date() })
        .where(eq(evaluationCriteria.id, criterionId));
    },

    async createPeriod(name) {
      const [period] = await transaction
        .insert(evaluationPeriods)
        .values({ name })
        .onConflictDoNothing()
        .returning({ id: evaluationPeriods.id });
      return period?.id ?? null;
    },

    async findPeriod(periodId) {
      const [period] = await transaction
        .select({
          id: evaluationPeriods.id,
          name: evaluationPeriods.name,
          status: evaluationPeriods.status,
        })
        .from(evaluationPeriods)
        .where(eq(evaluationPeriods.id, periodId))
        .limit(1);
      return period ?? null;
    },

    async closePeriod(periodId, closedAt) {
      const [closed] = await transaction
        .update(evaluationPeriods)
        .set({ status: 'Closed', closedAt })
        .where(and(
          eq(evaluationPeriods.id, periodId),
          eq(evaluationPeriods.status, 'Open'),
        ))
        .returning({ id: evaluationPeriods.id });
      return Boolean(closed);
    },

    async findActiveCandidate(candidateId) {
      const [candidate] = await transaction
        .select({
          id: probationCandidates.id,
          fullName: probationCandidates.fullName,
          teamId: probationCandidates.teamId,
        })
        .from(probationCandidates)
        .where(and(
          eq(probationCandidates.id, candidateId),
          eq(probationCandidates.status, 'Active'),
        ))
        .limit(1);
      return candidate ?? null;
    },

    async findCandidate(candidateId) {
      const [candidate] = await transaction
        .select({
          id: probationCandidates.id,
          fullName: probationCandidates.fullName,
          teamId: probationCandidates.teamId,
        })
        .from(probationCandidates)
        .where(eq(probationCandidates.id, candidateId))
        .limit(1);
      return candidate ?? null;
    },

    async findLinkedActiveCandidate(discordUserId) {
      const [candidate] = await transaction
        .select({
          id: probationCandidates.id,
          fullName: probationCandidates.fullName,
          teamId: probationCandidates.teamId,
        })
        .from(discordIdentityLinks)
        .innerJoin(probationCandidates, eq(discordIdentityLinks.subjectId, probationCandidates.id))
        .where(and(
          eq(discordIdentityLinks.discordUserId, discordUserId),
          eq(discordIdentityLinks.subjectType, 'ProbationCandidate'),
          eq(probationCandidates.status, 'Active'),
        ))
        .limit(1);
      return candidate ?? null;
    },

    async findLinkedActiveMember(discordUserId) {
      const [member] = await transaction
        .select({ id: members.id, fullName: members.fullName })
        .from(discordIdentityLinks)
        .innerJoin(members, eq(discordIdentityLinks.subjectId, members.id))
        .where(and(
          eq(discordIdentityLinks.discordUserId, discordUserId),
          eq(discordIdentityLinks.subjectType, 'Member'),
          eq(members.status, 'Active'),
        ))
        .limit(1);
      return member ?? null;
    },

    async listMentorTeamIds(memberId) {
      const rows = await transaction
        .select({ teamId: teamMentors.teamId })
        .from(teamMentors)
        .innerJoin(probationTeams, eq(probationTeams.id, teamMentors.teamId))
        .where(and(
          eq(teamMentors.memberId, memberId),
          eq(probationTeams.active, true),
        ));
      return rows.map(({ teamId }) => teamId);
    },

    async createSubmission(input) {
      if (input.kind === 'Peer') {
        const [created] = await transaction
          .insert(peerEvaluations)
          .values({
            periodId: input.periodId,
            evaluatorCandidateId: input.evaluatorCandidateId,
            targetCandidateId: input.targetCandidateId,
            scores: input.scores,
            note: input.note,
            evaluatorNameSnapshot: input.evaluatorNameSnapshot,
            targetNameSnapshot: input.targetNameSnapshot,
          })
          .onConflictDoNothing()
          .returning({ id: peerEvaluations.id });
        return created?.id ?? null;
      }

      const [created] = await transaction
        .insert(mentorEvaluations)
        .values({
          periodId: input.periodId,
          mentorMemberId: input.mentorMemberId,
          targetCandidateId: input.targetCandidateId,
          scores: input.scores,
          note: input.note,
          mentorNameSnapshot: input.evaluatorNameSnapshot,
          targetNameSnapshot: input.targetNameSnapshot,
        })
        .onConflictDoNothing()
        .returning({ id: mentorEvaluations.id });
      return created?.id ?? null;
    },

    async listRawEvaluations(input) {
      const peerFilter = input.targetCandidateId
        ? and(
          eq(peerEvaluations.periodId, input.periodId),
          eq(peerEvaluations.targetCandidateId, input.targetCandidateId),
        )
        : eq(peerEvaluations.periodId, input.periodId);
      const mentorFilter = input.targetCandidateId
        ? and(
          eq(mentorEvaluations.periodId, input.periodId),
          eq(mentorEvaluations.targetCandidateId, input.targetCandidateId),
        )
        : eq(mentorEvaluations.periodId, input.periodId);

      const peerRows = input.kind === 'Mentor'
        ? []
        : await transaction
          .select({
            id: peerEvaluations.id,
            periodId: peerEvaluations.periodId,
            evaluatorId: peerEvaluations.evaluatorCandidateId,
            evaluatorName: peerEvaluations.evaluatorNameSnapshot,
            targetCandidateId: peerEvaluations.targetCandidateId,
            targetName: peerEvaluations.targetNameSnapshot,
            scores: peerEvaluations.scores,
            note: peerEvaluations.note,
            submittedAt: peerEvaluations.createdAt,
          })
          .from(peerEvaluations)
          .where(peerFilter)
          .orderBy(asc(peerEvaluations.createdAt), asc(peerEvaluations.id));
      const mentorRows = input.kind === 'Peer'
        ? []
        : await transaction
          .select({
            id: mentorEvaluations.id,
            periodId: mentorEvaluations.periodId,
            evaluatorId: mentorEvaluations.mentorMemberId,
            evaluatorName: mentorEvaluations.mentorNameSnapshot,
            targetCandidateId: mentorEvaluations.targetCandidateId,
            targetName: mentorEvaluations.targetNameSnapshot,
            scores: mentorEvaluations.scores,
            note: mentorEvaluations.note,
            submittedAt: mentorEvaluations.createdAt,
          })
          .from(mentorEvaluations)
          .where(mentorFilter)
          .orderBy(asc(mentorEvaluations.createdAt), asc(mentorEvaluations.id));

      return [
        ...peerRows.map((row) => ({
          ...row,
          kind: 'Peer' as const,
          scores: toScoreSnapshots(row.scores),
        })),
        ...mentorRows.map((row) => ({
          ...row,
          kind: 'Mentor' as const,
          scores: toScoreSnapshots(row.scores),
        })),
      ];
    },

    async writeAudit(input) {
      await transaction.insert(auditLogs).values(input);
    },
  };
}

export function createDrizzleEvaluationPersistence(database: Database): EvaluationPersistence {
  return {
    transaction: <T>(work: (transaction: EvaluationTransaction) => Promise<T>): Promise<T> =>
      database.db.transaction((transaction) => work(createTransaction(transaction))),
  };
}
