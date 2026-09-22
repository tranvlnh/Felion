CREATE TYPE "public"."discord_role_mapping_kind" AS ENUM('Position', 'Probation', 'Department', 'Generation', 'ProbationTeam');--> statement-breakpoint
CREATE TYPE "public"."discord_subject_type" AS ENUM('Member', 'ProbationCandidate');--> statement-breakpoint
CREATE TYPE "public"."evaluation_period_status" AS ENUM('Open', 'Closed');--> statement-breakpoint
CREATE TYPE "public"."member_position" AS ENUM('Admin', 'Core', 'Member');--> statement-breakpoint
CREATE TYPE "public"."member_status" AS ENUM('Active', 'Inactive');--> statement-breakpoint
CREATE TYPE "public"."probation_candidate_status" AS ENUM('Active', 'Passed', 'Failed', 'Inactive');--> statement-breakpoint
CREATE TYPE "public"."sync_job_status" AS ENUM('Pending', 'Processing', 'Completed', 'Failed');--> statement-breakpoint
CREATE TYPE "public"."sync_operation" AS ENUM('KickUser');--> statement-breakpoint
CREATE TABLE "audit_logs" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"actor_discord_user_id" varchar(32),
	"action" varchar(100) NOT NULL,
	"entity_type" varchar(100) NOT NULL,
	"entity_id" varchar(100),
	"metadata" jsonb,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "departments" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"name" varchar(100) NOT NULL,
	"slug" varchar(100) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "discord_identity_links" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"discord_user_id" varchar(32) NOT NULL,
	"subject_type" "discord_subject_type" NOT NULL,
	"subject_id" uuid NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "discord_role_assignments" (
	"subject_type" "discord_subject_type" NOT NULL,
	"subject_id" uuid NOT NULL,
	"discord_role_id" varchar(32) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "discord_role_assignments_subject_type_subject_id_discord_role_id_pk" PRIMARY KEY("subject_type","subject_id","discord_role_id")
);
--> statement-breakpoint
CREATE TABLE "discord_role_mappings" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"kind" "discord_role_mapping_kind" NOT NULL,
	"key" varchar(200) NOT NULL,
	"discord_role_id" varchar(32) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "discord_sync_jobs" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"operation" "sync_operation" NOT NULL,
	"discord_user_id" varchar(32) NOT NULL,
	"status" "sync_job_status" DEFAULT 'Pending' NOT NULL,
	"attempts" integer DEFAULT 0 NOT NULL,
	"next_attempt_at" timestamp with time zone,
	"last_error" text,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "evaluation_periods" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"name" varchar(100) NOT NULL,
	"status" "evaluation_period_status" DEFAULT 'Open' NOT NULL,
	"opened_at" timestamp with time zone DEFAULT now() NOT NULL,
	"closed_at" timestamp with time zone
);
--> statement-breakpoint
CREATE TABLE "generations" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"name" varchar(100) NOT NULL,
	"code" varchar(50) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "members" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"student_id" varchar(32) NOT NULL,
	"full_name" varchar(200) NOT NULL,
	"club_email" varchar(320) NOT NULL,
	"department_id" uuid NOT NULL,
	"generation_id" uuid NOT NULL,
	"position" "member_position" DEFAULT 'Member' NOT NULL,
	"status" "member_status" DEFAULT 'Active' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "mentor_evaluations" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"period_id" uuid NOT NULL,
	"mentor_member_id" uuid NOT NULL,
	"target_candidate_id" uuid NOT NULL,
	"attendance" integer NOT NULL,
	"task_completion" integer NOT NULL,
	"learning_initiative" integer NOT NULL,
	"note" text,
	"mentor_name_snapshot" varchar(200) NOT NULL,
	"target_name_snapshot" varchar(200) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "peer_evaluations" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"period_id" uuid NOT NULL,
	"evaluator_candidate_id" uuid NOT NULL,
	"target_candidate_id" uuid NOT NULL,
	"contribution" integer NOT NULL,
	"communication" integer NOT NULL,
	"attitude" integer NOT NULL,
	"note" text,
	"evaluator_name_snapshot" varchar(200) NOT NULL,
	"target_name_snapshot" varchar(200) NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "probation_candidates" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"student_id" varchar(32) NOT NULL,
	"full_name" varchar(200) NOT NULL,
	"department_id" uuid NOT NULL,
	"generation_id" uuid NOT NULL,
	"team_id" uuid,
	"status" "probation_candidate_status" DEFAULT 'Active' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "probation_teams" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"name" varchar(100) NOT NULL,
	"discord_role_id" varchar(32),
	"active" boolean DEFAULT true NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "team_mentors" (
	"team_id" uuid NOT NULL,
	"member_id" uuid NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "team_mentors_team_id_member_id_pk" PRIMARY KEY("team_id","member_id")
);
--> statement-breakpoint
ALTER TABLE "members" ADD CONSTRAINT "members_department_id_departments_id_fk" FOREIGN KEY ("department_id") REFERENCES "public"."departments"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "members" ADD CONSTRAINT "members_generation_id_generations_id_fk" FOREIGN KEY ("generation_id") REFERENCES "public"."generations"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "mentor_evaluations" ADD CONSTRAINT "mentor_evaluations_period_id_evaluation_periods_id_fk" FOREIGN KEY ("period_id") REFERENCES "public"."evaluation_periods"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "mentor_evaluations" ADD CONSTRAINT "mentor_evaluations_mentor_member_id_members_id_fk" FOREIGN KEY ("mentor_member_id") REFERENCES "public"."members"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "mentor_evaluations" ADD CONSTRAINT "mentor_evaluations_target_candidate_id_probation_candidates_id_fk" FOREIGN KEY ("target_candidate_id") REFERENCES "public"."probation_candidates"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "peer_evaluations" ADD CONSTRAINT "peer_evaluations_period_id_evaluation_periods_id_fk" FOREIGN KEY ("period_id") REFERENCES "public"."evaluation_periods"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "peer_evaluations" ADD CONSTRAINT "peer_evaluations_evaluator_candidate_id_probation_candidates_id_fk" FOREIGN KEY ("evaluator_candidate_id") REFERENCES "public"."probation_candidates"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "peer_evaluations" ADD CONSTRAINT "peer_evaluations_target_candidate_id_probation_candidates_id_fk" FOREIGN KEY ("target_candidate_id") REFERENCES "public"."probation_candidates"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "probation_candidates" ADD CONSTRAINT "probation_candidates_department_id_departments_id_fk" FOREIGN KEY ("department_id") REFERENCES "public"."departments"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "probation_candidates" ADD CONSTRAINT "probation_candidates_generation_id_generations_id_fk" FOREIGN KEY ("generation_id") REFERENCES "public"."generations"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "probation_candidates" ADD CONSTRAINT "probation_candidates_team_id_probation_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."probation_teams"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "team_mentors" ADD CONSTRAINT "team_mentors_team_id_probation_teams_id_fk" FOREIGN KEY ("team_id") REFERENCES "public"."probation_teams"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "team_mentors" ADD CONSTRAINT "team_mentors_member_id_members_id_fk" FOREIGN KEY ("member_id") REFERENCES "public"."members"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
CREATE UNIQUE INDEX "departments_slug_unique" ON "departments" USING btree ("slug");--> statement-breakpoint
CREATE UNIQUE INDEX "discord_identity_links_user_unique" ON "discord_identity_links" USING btree ("discord_user_id");--> statement-breakpoint
CREATE UNIQUE INDEX "discord_identity_links_subject_unique" ON "discord_identity_links" USING btree ("subject_type","subject_id");--> statement-breakpoint
CREATE UNIQUE INDEX "discord_role_mappings_kind_key_unique" ON "discord_role_mappings" USING btree ("kind","key");--> statement-breakpoint
CREATE UNIQUE INDEX "evaluation_periods_name_unique" ON "evaluation_periods" USING btree ("name");--> statement-breakpoint
CREATE UNIQUE INDEX "generations_code_unique" ON "generations" USING btree ("code");--> statement-breakpoint
CREATE UNIQUE INDEX "members_student_id_unique" ON "members" USING btree ("student_id");--> statement-breakpoint
CREATE UNIQUE INDEX "members_club_email_unique" ON "members" USING btree ("club_email");--> statement-breakpoint
CREATE UNIQUE INDEX "mentor_evaluations_unique" ON "mentor_evaluations" USING btree ("period_id","mentor_member_id","target_candidate_id");--> statement-breakpoint
CREATE UNIQUE INDEX "peer_evaluations_unique" ON "peer_evaluations" USING btree ("period_id","evaluator_candidate_id","target_candidate_id");--> statement-breakpoint
CREATE UNIQUE INDEX "probation_candidates_student_id_unique" ON "probation_candidates" USING btree ("student_id");--> statement-breakpoint
CREATE UNIQUE INDEX "probation_teams_name_unique" ON "probation_teams" USING btree ("name");