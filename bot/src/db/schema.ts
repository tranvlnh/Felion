import {
  boolean,
  integer,
  jsonb,
  pgEnum,
  pgTable,
  primaryKey,
  text,
  timestamp,
  uniqueIndex,
  uuid,
  varchar,
} from 'drizzle-orm/pg-core';

export const memberPosition = pgEnum('member_position', ['Admin', 'Core', 'Member']);
export const memberStatus = pgEnum('member_status', ['Active', 'Inactive']);
export const probationCandidateStatus = pgEnum('probation_candidate_status', ['Active', 'Passed', 'Failed', 'Inactive']);
export const evaluationPeriodStatus = pgEnum('evaluation_period_status', ['Open', 'Closed']);
export const evaluationCriterionKind = pgEnum('evaluation_criterion_kind', ['Peer', 'Mentor']);
export const discordSubjectType = pgEnum('discord_subject_type', ['Member', 'ProbationCandidate']);
export const discordRoleMappingKind = pgEnum('discord_role_mapping_kind', ['Position', 'Probation', 'Department', 'Generation', 'ProbationTeam']);
export const syncOperation = pgEnum('sync_operation', ['KickUser']);
export const syncJobStatus = pgEnum('sync_job_status', ['Pending', 'Processing', 'Completed', 'Failed']);

const timestamps = {
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
  updatedAt: timestamp('updated_at', { withTimezone: true }).notNull().defaultNow(),
};

export const departments = pgTable('departments', {
  id: uuid('id').defaultRandom().primaryKey(),
  name: varchar('name', { length: 100 }).notNull(),
  slug: varchar('slug', { length: 100 }).notNull(),
  active: boolean('active').notNull().default(true),
  ...timestamps,
}, (table) => [uniqueIndex('departments_slug_unique').on(table.slug)]);

export const generations = pgTable('generations', {
  id: uuid('id').defaultRandom().primaryKey(),
  name: varchar('name', { length: 100 }).notNull(),
  active: boolean('active').notNull().default(true),
}, (table) => [uniqueIndex('generations_name_unique').on(table.name)]);

export const members = pgTable('members', {
  id: uuid('id').defaultRandom().primaryKey(),
  studentId: varchar('student_id', { length: 32 }).notNull(),
  fullName: varchar('full_name', { length: 200 }).notNull(),
  clubEmail: varchar('club_email', { length: 320 }).notNull(),
  departmentId: uuid('department_id').notNull().references(() => departments.id),
  generationId: uuid('generation_id').notNull().references(() => generations.id),
  position: memberPosition('position').notNull().default('Member'),
  status: memberStatus('status').notNull().default('Active'),
  ...timestamps,
}, (table) => [
  uniqueIndex('members_student_id_unique').on(table.studentId),
  uniqueIndex('members_club_email_unique').on(table.clubEmail),
]);

// This registry enforces the cross-aggregate rule that one StudentId can identify
// only one active Member or ProbationCandidate.
export const identityRegistry = pgTable('identity_registry', {
  studentId: varchar('student_id', { length: 32 }).primaryKey(),
  subjectType: discordSubjectType('subject_type').notNull(),
  subjectId: uuid('subject_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

export const probationTeams = pgTable('probation_teams', {
  id: uuid('id').defaultRandom().primaryKey(),
  name: varchar('name', { length: 100 }).notNull(),
  discordRoleId: varchar('discord_role_id', { length: 32 }),
  active: boolean('active').notNull().default(true),
  ...timestamps,
}, (table) => [uniqueIndex('probation_teams_name_unique').on(table.name)]);

export const probationCandidates = pgTable('probation_candidates', {
  id: uuid('id').defaultRandom().primaryKey(),
  studentId: varchar('student_id', { length: 32 }).notNull(),
  fullName: varchar('full_name', { length: 200 }).notNull(),
  departmentId: uuid('department_id').notNull().references(() => departments.id),
  generationId: uuid('generation_id').notNull().references(() => generations.id),
  teamId: uuid('team_id').references(() => probationTeams.id),
  status: probationCandidateStatus('status').notNull().default('Active'),
  ...timestamps,
}, (table) => [uniqueIndex('probation_candidates_student_id_unique').on(table.studentId)]);

export const teamMentors = pgTable('team_mentors', {
  teamId: uuid('team_id').notNull().references(() => probationTeams.id),
  memberId: uuid('member_id').notNull().references(() => members.id),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (table) => [primaryKey({ columns: [table.teamId, table.memberId] })]);

export const discordIdentityLinks = pgTable('discord_identity_links', {
  id: uuid('id').defaultRandom().primaryKey(),
  discordUserId: varchar('discord_user_id', { length: 32 }).notNull(),
  subjectType: discordSubjectType('subject_type').notNull(),
  subjectId: uuid('subject_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (table) => [
  uniqueIndex('discord_identity_links_user_unique').on(table.discordUserId),
  uniqueIndex('discord_identity_links_subject_unique').on(table.subjectType, table.subjectId),
]);

export const discordRoleMappings = pgTable('discord_role_mappings', {
  id: uuid('id').defaultRandom().primaryKey(),
  kind: discordRoleMappingKind('kind').notNull(),
  key: varchar('key', { length: 200 }).notNull(),
  discordRoleId: varchar('discord_role_id', { length: 32 }).notNull(),
  ...timestamps,
}, (table) => [uniqueIndex('discord_role_mappings_kind_key_unique').on(table.kind, table.key)]);

export const discordRoleAssignments = pgTable('discord_role_assignments', {
  subjectType: discordSubjectType('subject_type').notNull(),
  subjectId: uuid('subject_id').notNull(),
  discordRoleId: varchar('discord_role_id', { length: 32 }).notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (table) => [primaryKey({ columns: [table.subjectType, table.subjectId, table.discordRoleId] })]);

export const evaluationPeriods = pgTable('evaluation_periods', {
  id: uuid('id').defaultRandom().primaryKey(),
  name: varchar('name', { length: 100 }).notNull(),
  status: evaluationPeriodStatus('status').notNull().default('Open'),
  openedAt: timestamp('opened_at', { withTimezone: true }).notNull().defaultNow(),
  closedAt: timestamp('closed_at', { withTimezone: true }),
}, (table) => [uniqueIndex('evaluation_periods_name_unique').on(table.name)]);

export const evaluationCriteria = pgTable('evaluation_criteria', {
  id: uuid('id').defaultRandom().primaryKey(),
  kind: evaluationCriterionKind('kind').notNull(),
  key: varchar('key', { length: 100 }).notNull(),
  name: varchar('name', { length: 100 }).notNull(),
  minScore: integer('min_score').notNull().default(1),
  maxScore: integer('max_score').notNull(),
  sortOrder: integer('sort_order').notNull().default(0),
  active: boolean('active').notNull().default(true),
  ...timestamps,
}, (table) => [uniqueIndex('evaluation_criteria_kind_key_unique').on(table.kind, table.key)]);

export const peerEvaluations = pgTable('peer_evaluations', {
  id: uuid('id').defaultRandom().primaryKey(),
  periodId: uuid('period_id').notNull().references(() => evaluationPeriods.id),
  evaluatorCandidateId: uuid('evaluator_candidate_id').notNull().references(() => probationCandidates.id),
  targetCandidateId: uuid('target_candidate_id').notNull().references(() => probationCandidates.id),
  scores: jsonb('scores').notNull(),
  note: text('note'),
  evaluatorNameSnapshot: varchar('evaluator_name_snapshot', { length: 200 }).notNull(),
  targetNameSnapshot: varchar('target_name_snapshot', { length: 200 }).notNull(),
  ...timestamps,
}, (table) => [uniqueIndex('peer_evaluations_unique').on(table.periodId, table.evaluatorCandidateId, table.targetCandidateId)]);

export const mentorEvaluations = pgTable('mentor_evaluations', {
  id: uuid('id').defaultRandom().primaryKey(),
  periodId: uuid('period_id').notNull().references(() => evaluationPeriods.id),
  mentorMemberId: uuid('mentor_member_id').notNull().references(() => members.id),
  targetCandidateId: uuid('target_candidate_id').notNull().references(() => probationCandidates.id),
  scores: jsonb('scores').notNull(),
  note: text('note'),
  mentorNameSnapshot: varchar('mentor_name_snapshot', { length: 200 }).notNull(),
  targetNameSnapshot: varchar('target_name_snapshot', { length: 200 }).notNull(),
  ...timestamps,
}, (table) => [uniqueIndex('mentor_evaluations_unique').on(table.periodId, table.mentorMemberId, table.targetCandidateId)]);

export const auditLogs = pgTable('audit_logs', {
  id: uuid('id').defaultRandom().primaryKey(),
  actorDiscordUserId: varchar('actor_discord_user_id', { length: 32 }),
  action: varchar('action', { length: 100 }).notNull(),
  entityType: varchar('entity_type', { length: 100 }).notNull(),
  entityId: varchar('entity_id', { length: 100 }),
  metadata: jsonb('metadata'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
});

export const discordSyncJobs = pgTable('discord_sync_jobs', {
  id: uuid('id').defaultRandom().primaryKey(),
  operation: syncOperation('operation').notNull(),
  discordUserId: varchar('discord_user_id', { length: 32 }).notNull(),
  status: syncJobStatus('status').notNull().default('Pending'),
  attempts: integer('attempts').notNull().default(0),
  nextAttemptAt: timestamp('next_attempt_at', { withTimezone: true }),
  lastError: text('last_error'),
  ...timestamps,
});
