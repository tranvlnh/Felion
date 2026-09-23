import type {
  EvaluationCriterion,
  EvaluationKind,
  EvaluationScoreSnapshot,
} from '#app/domain/evaluation.js';

export type EvaluationPeriodRecord = {
  id: string;
  name: string;
  status: 'Open' | 'Closed';
};

export type EvaluationCandidateRecord = {
  id: string;
  fullName: string;
  teamId: string | null;
};

export type EvaluationMemberRecord = {
  id: string;
  fullName: string;
};

export type RawEvaluationRecord = {
  id: string;
  kind: EvaluationKind;
  periodId: string;
  evaluatorId: string;
  evaluatorName: string;
  targetCandidateId: string;
  targetName: string;
  scores: readonly EvaluationScoreSnapshot[];
  note: string | null;
  submittedAt: Date;
};

export type EvaluationAuditInput = {
  actorDiscordUserId: string;
  action: string;
  entityType: string;
  entityId: string;
  metadata: Record<string, unknown>;
};

export type EvaluationCriterionCreateInput = {
  kind: EvaluationKind;
  key: string;
  name: string;
  maxScore: number;
  sortOrder: number;
};

type EvaluationSubmissionRecordBase = {
  periodId: string;
  targetCandidateId: string;
  scores: readonly EvaluationScoreSnapshot[];
  note: string | null;
  evaluatorNameSnapshot: string;
  targetNameSnapshot: string;
};

export type EvaluationSubmissionRecord =
  | (EvaluationSubmissionRecordBase & {
    kind: 'Peer';
    evaluatorCandidateId: string;
  })
  | (EvaluationSubmissionRecordBase & {
    kind: 'Mentor';
    mentorMemberId: string;
  });

export interface EvaluationTransaction {
  listCriteria(kind: EvaluationKind): Promise<EvaluationCriterion[]>;
  listActiveCriteria(kind: EvaluationKind): Promise<EvaluationCriterion[]>;
  findCriterion(criterionId: string): Promise<EvaluationCriterion | null>;
  createCriterion(input: EvaluationCriterionCreateInput): Promise<string | null>;
  reactivateCriterion(criterionId: string, name: string): Promise<boolean>;
  renameCriterion(criterionId: string, name: string): Promise<void>;
  deactivateCriterion(criterionId: string): Promise<void>;

  createPeriod(name: string): Promise<string | null>;
  findPeriod(periodId: string): Promise<EvaluationPeriodRecord | null>;
  closePeriod(periodId: string, closedAt: Date): Promise<boolean>;

  findActiveCandidate(candidateId: string): Promise<EvaluationCandidateRecord | null>;
  findCandidate(candidateId: string): Promise<EvaluationCandidateRecord | null>;
  findLinkedActiveCandidate(discordUserId: string): Promise<EvaluationCandidateRecord | null>;
  findLinkedActiveMember(discordUserId: string): Promise<EvaluationMemberRecord | null>;
  listMentorTeamIds(memberId: string): Promise<string[]>;
  createSubmission(input: EvaluationSubmissionRecord): Promise<string | null>;
  listRawEvaluations(input: {
    periodId: string;
    targetCandidateId?: string;
    kind?: EvaluationKind;
  }): Promise<RawEvaluationRecord[]>;

  writeAudit(input: EvaluationAuditInput): Promise<void>;
}

export interface EvaluationPersistence {
  transaction<T>(work: (transaction: EvaluationTransaction) => Promise<T>): Promise<T>;
}
