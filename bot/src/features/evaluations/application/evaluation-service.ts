import {
  assertCanDeactivateCriterion,
  assertUniqueActiveCriterionName,
  criterionKey,
  normalizeCriterionName,
  normalizeEvaluationNote,
  normalizeEvaluationPeriodName,
  type EvaluationCriterion,
  type EvaluationKind,
  validateScoreSnapshots,
} from '#app/domain/evaluation.js';
import {
  assertMentorEvaluationAllowed,
  assertPeerEvaluationAllowed,
} from '#app/domain/probation.js';
import { normalizeReferenceId } from '#app/domain/reference-data.js';
import type {
  EvaluationPersistence,
  EvaluationPeriodRecord,
  EvaluationSubmissionRecord,
  EvaluationTransaction,
  RawEvaluationRecord,
} from './contracts.js';

const maxScoreByKind: Record<EvaluationKind, number> = { Peer: 5, Mentor: 10 };

export type EvaluationAdminActor = {
  discordUserId: string;
};

export type EvaluationReportActor = {
  discordUserId: string;
  position: 'Admin' | 'Core';
};

export type RawEvaluationReport = {
  period: EvaluationPeriodRecord;
  target: { id: string; fullName: string } | null;
  evaluations: RawEvaluationRecord[];
};

export type EvaluationScoreInput = {
  criterionId: string;
  score: number;
};

export type EvaluationSubmissionContext = {
  kind: EvaluationKind;
  periodId: string;
  actorDiscordUserId: string;
  evaluatorId: string;
  targetCandidateId: string;
  evaluatorName: string;
  targetName: string;
  criteria: EvaluationCriterion[];
};

type EvaluationSubmissionInput = {
  kind: EvaluationKind;
  periodId: string;
  targetCandidateId: string;
  actorDiscordUserId: string;
  scores: readonly EvaluationScoreInput[];
  note?: string | null;
};

export interface EvaluationService {
  addCriterion(actor: EvaluationAdminActor, input: { kind: EvaluationKind; name: string }): Promise<void>;
  renameCriterion(actor: EvaluationAdminActor, input: { criterionId: string; name: string }): Promise<void>;
  deactivateCriterion(actor: EvaluationAdminActor, input: { criterionId: string }): Promise<void>;
  openPeriod(actor: EvaluationAdminActor, input: { name: string }): Promise<void>;
  closePeriod(actor: EvaluationAdminActor, input: { periodId: string }): Promise<void>;
  getSubmissionContext(input: {
    kind: EvaluationKind;
    periodId: string;
    targetCandidateId: string;
    actorDiscordUserId: string;
  }): Promise<EvaluationSubmissionContext>;
  submitPeerEvaluation(input: Omit<EvaluationSubmissionInput, 'kind'>): Promise<string>;
  submitMentorEvaluation(input: Omit<EvaluationSubmissionInput, 'kind'>): Promise<string>;
  getRawReport(actor: EvaluationReportActor, input: {
    periodId: string;
    targetCandidateId?: string;
    kind?: EvaluationKind;
  }): Promise<RawEvaluationReport>;
}

async function loadSubmissionContext(
  transaction: EvaluationTransaction,
  input: {
    kind: EvaluationKind;
    periodId: string;
    targetCandidateId: string;
    actorDiscordUserId: string;
  },
): Promise<EvaluationSubmissionContext> {
  const period = await transaction.findPeriod(input.periodId);
  if (!period) {
    throw new Error('Evaluation period was not found.');
  }
  if (period.status !== 'Open') {
    throw new Error('Evaluation period is closed.');
  }

  const target = await transaction.findActiveCandidate(input.targetCandidateId);
  if (!target) {
    throw new Error('An active target ProbationCandidate was not found.');
  }

  const criteria = await transaction.listActiveCriteria(input.kind);
  if (criteria.length === 0) {
    throw new Error(`No active ${input.kind} evaluation criteria are configured.`);
  }

  if (input.kind === 'Peer') {
    const evaluator = await transaction.findLinkedActiveCandidate(input.actorDiscordUserId);
    if (!evaluator) {
      throw new Error('Only an active linked ProbationCandidate may submit a Peer evaluation.');
    }

    assertPeerEvaluationAllowed(evaluator.id, target.id, evaluator.teamId, target.teamId);
    return {
      ...input,
      evaluatorId: evaluator.id,
      targetCandidateId: target.id,
      evaluatorName: evaluator.fullName,
      targetName: target.fullName,
      criteria,
    };
  }

  const evaluator = await transaction.findLinkedActiveMember(input.actorDiscordUserId);
  if (!evaluator) {
    throw new Error('Only an active linked Member may submit a Mentor evaluation.');
  }

  const mentorTeamIds = await transaction.listMentorTeamIds(evaluator.id);
  assertMentorEvaluationAllowed(evaluator.id, mentorTeamIds, target.teamId);
  return {
    ...input,
    evaluatorId: evaluator.id,
    targetCandidateId: target.id,
    evaluatorName: evaluator.fullName,
    targetName: target.fullName,
    criteria,
  };
}

export function createEvaluationService(persistence: EvaluationPersistence): EvaluationService {
  async function addCriterion(
    actor: EvaluationAdminActor,
    input: { kind: EvaluationKind; name: string },
  ): Promise<void> {
    const name = normalizeCriterionName(input.name);
    const key = criterionKey(name);

    await persistence.transaction(async (transaction) => {
      const criteria = await transaction.listCriteria(input.kind);
      const existing = criteria.find((criterion) => criterion.key === key);
      assertUniqueActiveCriterionName(criteria, input.kind, name, existing?.id);

      let criterionId: string;
      let action: string;
      if (existing) {
        if (!(await transaction.reactivateCriterion(existing.id, name))) {
          throw new Error('Unable to reactivate evaluation criterion.');
        }
        criterionId = existing.id;
        action = 'EvaluationCriterionReactivated';
      } else {
        const createdId = await transaction.createCriterion({
          kind: input.kind,
          key,
          name,
          maxScore: maxScoreByKind[input.kind],
          sortOrder: criteria.length,
        });
        if (!createdId) {
          throw new Error('Unable to create evaluation criterion.');
        }
        criterionId = createdId;
        action = 'EvaluationCriterionCreated';
      }

      await transaction.writeAudit({
        actorDiscordUserId: actor.discordUserId,
        action,
        entityType: 'EvaluationCriterion',
        entityId: criterionId,
        metadata: { kind: input.kind, name, key },
      });
    });
  }

  async function renameCriterion(
    actor: EvaluationAdminActor,
    input: { criterionId: string; name: string },
  ): Promise<void> {
    const name = normalizeCriterionName(input.name);

    await persistence.transaction(async (transaction) => {
      const criterion = await transaction.findCriterion(input.criterionId);
      if (!criterion || !criterion.active) {
        throw new Error('Active evaluation criterion was not found.');
      }

      const criteria = await transaction.listCriteria(criterion.kind);
      assertUniqueActiveCriterionName(criteria, criterion.kind, name, criterion.id);
      await transaction.renameCriterion(criterion.id, name);
      await transaction.writeAudit({
        actorDiscordUserId: actor.discordUserId,
        action: 'EvaluationCriterionRenamed',
        entityType: 'EvaluationCriterion',
        entityId: criterion.id,
        metadata: { kind: criterion.kind, previousName: criterion.name, name },
      });
    });
  }

  async function deactivateCriterion(
    actor: EvaluationAdminActor,
    input: { criterionId: string },
  ): Promise<void> {
    await persistence.transaction(async (transaction) => {
      const criterion = await transaction.findCriterion(input.criterionId);
      if (!criterion || !criterion.active) {
        throw new Error('Active evaluation criterion was not found.');
      }

      const activeCriteria = await transaction.listActiveCriteria(criterion.kind);
      assertCanDeactivateCriterion(activeCriteria.length);
      await transaction.deactivateCriterion(criterion.id);
      await transaction.writeAudit({
        actorDiscordUserId: actor.discordUserId,
        action: 'EvaluationCriterionDeactivated',
        entityType: 'EvaluationCriterion',
        entityId: criterion.id,
        metadata: { kind: criterion.kind, name: criterion.name },
      });
    });
  }

  async function openPeriod(actor: EvaluationAdminActor, input: { name: string }): Promise<void> {
    const name = normalizeEvaluationPeriodName(input.name);
    await persistence.transaction(async (transaction) => {
      const periodId = await transaction.createPeriod(name);
      if (!periodId) {
        throw new Error('An evaluation period with this name already exists.');
      }
      await transaction.writeAudit({
        actorDiscordUserId: actor.discordUserId,
        action: 'EvaluationPeriodOpened',
        entityType: 'EvaluationPeriod',
        entityId: periodId,
        metadata: { name },
      });
    });
  }

  async function closePeriod(actor: EvaluationAdminActor, input: { periodId: string }): Promise<void> {
    const periodId = normalizeReferenceId(input.periodId);
    await persistence.transaction(async (transaction) => {
      const period = await transaction.findPeriod(periodId);
      if (!period) {
        throw new Error('Evaluation period was not found.');
      }
      if (period.status !== 'Open') {
        throw new Error('Evaluation period is already closed.');
      }
      if (!(await transaction.closePeriod(periodId, new Date()))) {
        throw new Error('Evaluation period is no longer open.');
      }
      await transaction.writeAudit({
        actorDiscordUserId: actor.discordUserId,
        action: 'EvaluationPeriodClosed',
        entityType: 'EvaluationPeriod',
        entityId: periodId,
        metadata: { name: period.name },
      });
    });
  }

  async function getSubmissionContext(input: {
    kind: EvaluationKind;
    periodId: string;
    targetCandidateId: string;
    actorDiscordUserId: string;
  }): Promise<EvaluationSubmissionContext> {
    const periodId = normalizeReferenceId(input.periodId);
    const targetCandidateId = normalizeReferenceId(input.targetCandidateId);
    return persistence.transaction((transaction) => loadSubmissionContext(transaction, {
      ...input,
      periodId,
      targetCandidateId,
    }));
  }

  async function submitEvaluation(input: EvaluationSubmissionInput): Promise<string> {
    const periodId = normalizeReferenceId(input.periodId);
    const targetCandidateId = normalizeReferenceId(input.targetCandidateId);
    const note = normalizeEvaluationNote(input.note);

    return persistence.transaction(async (transaction) => {
      const context = await loadSubmissionContext(transaction, {
        kind: input.kind,
        periodId,
        targetCandidateId,
        actorDiscordUserId: input.actorDiscordUserId,
      });
      const snapshots = input.scores.map((score) => {
        const criterion = context.criteria.find((candidate) => candidate.id === score.criterionId);
        return {
          criterionId: score.criterionId,
          criterionName: criterion?.name ?? '',
          score: score.score,
        };
      });
      validateScoreSnapshots(context.criteria, snapshots);

      const submission: EvaluationSubmissionRecord = input.kind === 'Peer'
        ? {
          kind: 'Peer',
          periodId,
          targetCandidateId: context.targetCandidateId,
          evaluatorCandidateId: context.evaluatorId,
          evaluatorNameSnapshot: context.evaluatorName,
          targetNameSnapshot: context.targetName,
          scores: snapshots,
          note,
        }
        : {
          kind: 'Mentor',
          periodId,
          targetCandidateId: context.targetCandidateId,
          mentorMemberId: context.evaluatorId,
          evaluatorNameSnapshot: context.evaluatorName,
          targetNameSnapshot: context.targetName,
          scores: snapshots,
          note,
        };
      const submissionId = await transaction.createSubmission(submission);
      if (!submissionId) {
        throw new Error(`This ${input.kind} evaluation has already been submitted for the selected target in this period.`);
      }

      await transaction.writeAudit({
        actorDiscordUserId: input.actorDiscordUserId,
        action: `${input.kind}EvaluationSubmitted`,
        entityType: input.kind === 'Peer' ? 'PeerEvaluation' : 'MentorEvaluation',
        entityId: submissionId,
        metadata: {
          periodId,
          evaluatorId: context.evaluatorId,
          targetCandidateId: context.targetCandidateId,
          criterionCount: snapshots.length,
          hasNote: note !== null,
        },
      });
      return submissionId;
    });
  }

  async function getRawReport(
    _actor: EvaluationReportActor,
    input: {
      periodId: string;
      targetCandidateId?: string;
      kind?: EvaluationKind;
    },
  ): Promise<RawEvaluationReport> {
    const periodId = normalizeReferenceId(input.periodId);
    const targetCandidateId = input.targetCandidateId
      ? normalizeReferenceId(input.targetCandidateId)
      : undefined;

    return persistence.transaction(async (transaction) => {
      const period = await transaction.findPeriod(periodId);
      if (!period) {
        throw new Error('Evaluation period was not found.');
      }

      const target = targetCandidateId
        ? await transaction.findCandidate(targetCandidateId)
        : null;
      if (targetCandidateId && !target) {
        throw new Error('ProbationCandidate was not found.');
      }

      const evaluations = await transaction.listRawEvaluations({
        periodId,
        ...(targetCandidateId ? { targetCandidateId } : {}),
        ...(input.kind ? { kind: input.kind } : {}),
      });
      evaluations.sort((left, right) =>
        left.submittedAt.getTime() - right.submittedAt.getTime()
        || left.id.localeCompare(right.id));

      return {
        period,
        target: target ? { id: target.id, fullName: target.fullName } : null,
        evaluations,
      };
    });
  }

  return {
    addCriterion,
    renameCriterion,
    deactivateCriterion,
    openPeriod,
    closePeriod,
    getSubmissionContext,
    submitPeerEvaluation: (input) => submitEvaluation({ ...input, kind: 'Peer' }),
    submitMentorEvaluation: (input) => submitEvaluation({ ...input, kind: 'Mentor' }),
    getRawReport,
  };
}
