import { and, eq, or } from 'drizzle-orm';
import {
  assertProbationCandidateTransition,
  createProbationCandidate,
} from '#app/domain/probation.js';
import { normalizeReferenceId } from '#app/domain/reference-data.js';
import type { Database, DbTransaction } from './client.js';
import {
  auditLogs,
  departments,
  discordIdentityLinks,
  generations,
  identityRegistry,
  probationCandidates,
  probationTeams,
} from './schema.js';

export type CreateProbationCandidateInput = {
  studentId: string;
  fullName: string;
  department: string;
  generation: string;
  actorDiscordUserId: string;
};

export type ProbationCandidateMutationResult = {
  candidateId: string;
  discordUserId: string | null;
};

export async function createActiveProbationCandidate(
  database: Database,
  input: CreateProbationCandidateInput,
): Promise<string> {
  return database.db.transaction(async (transaction) => {
    const department = (await transaction
      .select()
      .from(departments)
      .where(and(
        eq(departments.active, true),
        or(
          eq(departments.slug, input.department.trim().toLowerCase()),
          eq(departments.name, input.department.trim()),
        ),
      ))
      .limit(1))[0];
    const generation = (await transaction
      .select()
      .from(generations)
      .where(and(eq(generations.name, input.generation.trim()), eq(generations.active, true)))
      .limit(1))[0];

    if (!department || !generation) {
      throw new Error('An active Department or Generation was not found.');
    }

    const candidate = createProbationCandidate({
      studentId: input.studentId,
      fullName: input.fullName,
      departmentId: department.id,
      generationId: generation.id,
    });

    await transaction.insert(probationCandidates).values(candidate);
    await transaction.insert(identityRegistry).values({
      studentId: candidate.studentId,
      subjectType: 'ProbationCandidate',
      subjectId: candidate.id,
    });
    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationCandidateCreated',
      entityType: 'ProbationCandidate',
      entityId: candidate.id,
      metadata: { studentId: candidate.studentId },
    });

    return candidate.id;
  });
}

export async function assignProbationCandidateTeam(
  database: Database,
  input: { candidateId: string; teamId: string; actorDiscordUserId: string },
): Promise<ProbationCandidateMutationResult> {
  const candidateId = normalizeReferenceId(input.candidateId);
  const teamId = normalizeReferenceId(input.teamId);

  return database.db.transaction(async (transaction) => {
    const candidate = (await transaction
      .select({ id: probationCandidates.id, status: probationCandidates.status, teamId: probationCandidates.teamId })
      .from(probationCandidates)
      .where(eq(probationCandidates.id, candidateId))
      .limit(1))[0];
    if (!candidate) {
      throw new Error('ProbationCandidate was not found.');
    }
    if (candidate.status !== 'Active') {
      throw new Error('Only an active ProbationCandidate can be assigned to a team.');
    }
    if (candidate.teamId === teamId) {
      throw new Error('ProbationCandidate is already assigned to this team.');
    }

    const team = (await transaction
      .select({ id: probationTeams.id })
      .from(probationTeams)
      .where(and(eq(probationTeams.id, teamId), eq(probationTeams.active, true)))
      .limit(1))[0];
    if (!team) {
      throw new Error('An active probation team was not found.');
    }

    const updated = (await transaction
      .update(probationCandidates)
      .set({ teamId, updatedAt: new Date() })
      .where(and(eq(probationCandidates.id, candidateId), eq(probationCandidates.status, 'Active')))
      .returning({ id: probationCandidates.id }))[0];
    if (!updated) {
      throw new Error('ProbationCandidate is no longer active.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: 'ProbationCandidateTeamAssigned',
      entityType: 'ProbationCandidate',
      entityId: candidateId,
      metadata: { previousTeamId: candidate.teamId, teamId },
    });

    return {
      candidateId,
      discordUserId: await findCandidateDiscordUserId(transaction, candidateId),
    };
  });
}

export async function deactivateProbationCandidate(
  database: Database,
  input: { candidateId: string; actorDiscordUserId: string },
): Promise<ProbationCandidateMutationResult> {
  return changeProbationCandidateStatus(database, input, 'Inactive');
}

export async function reactivateProbationCandidate(
  database: Database,
  input: { candidateId: string; actorDiscordUserId: string },
): Promise<ProbationCandidateMutationResult> {
  return changeProbationCandidateStatus(database, input, 'Active');
}

async function changeProbationCandidateStatus(
  database: Database,
  input: { candidateId: string; actorDiscordUserId: string },
  targetStatus: 'Active' | 'Inactive',
): Promise<ProbationCandidateMutationResult> {
  const candidateId = normalizeReferenceId(input.candidateId);

  return database.db.transaction(async (transaction) => {
    const candidate = (await transaction
      .select({ status: probationCandidates.status })
      .from(probationCandidates)
      .where(eq(probationCandidates.id, candidateId))
      .limit(1))[0];
    if (!candidate) {
      throw new Error('ProbationCandidate was not found.');
    }
    assertProbationCandidateTransition(candidate.status, targetStatus);

    const updated = (await transaction
      .update(probationCandidates)
      .set({ status: targetStatus, updatedAt: new Date() })
      .where(and(eq(probationCandidates.id, candidateId), eq(probationCandidates.status, candidate.status)))
      .returning({ id: probationCandidates.id }))[0];
    if (!updated) {
      throw new Error('ProbationCandidate status changed concurrently.');
    }

    await transaction.insert(auditLogs).values({
      actorDiscordUserId: input.actorDiscordUserId,
      action: targetStatus === 'Active'
        ? 'ProbationCandidateReactivated'
        : 'ProbationCandidateDeactivated',
      entityType: 'ProbationCandidate',
      entityId: candidateId,
      metadata: { previousStatus: candidate.status, status: targetStatus },
    });

    return {
      candidateId,
      discordUserId: await findCandidateDiscordUserId(transaction, candidateId),
    };
  });
}

async function findCandidateDiscordUserId(
  transaction: DbTransaction,
  candidateId: string,
): Promise<string | null> {
  const link = (await transaction
    .select({ discordUserId: discordIdentityLinks.discordUserId })
    .from(discordIdentityLinks)
    .where(and(
      eq(discordIdentityLinks.subjectType, 'ProbationCandidate'),
      eq(discordIdentityLinks.subjectId, candidateId),
    ))
    .limit(1))[0];

  return link?.discordUserId ?? null;
}
