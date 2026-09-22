CREATE TYPE "public"."evaluation_criterion_kind" AS ENUM('Peer', 'Mentor');--> statement-breakpoint

CREATE TABLE "evaluation_criteria" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"kind" "evaluation_criterion_kind" NOT NULL,
	"key" varchar(100) NOT NULL,
	"name" varchar(100) NOT NULL,
	"min_score" integer DEFAULT 1 NOT NULL,
	"max_score" integer NOT NULL,
	"sort_order" integer DEFAULT 0 NOT NULL,
	"active" boolean DEFAULT true NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);--> statement-breakpoint

CREATE UNIQUE INDEX "evaluation_criteria_kind_key_unique" ON "evaluation_criteria" USING btree ("kind", "key");--> statement-breakpoint

INSERT INTO "evaluation_criteria" ("id", "kind", "key", "name", "max_score", "sort_order") VALUES
	('00000000-0000-0000-0000-000000000001', 'Peer', 'contribution', 'Contribution', 5, 0),
	('00000000-0000-0000-0000-000000000002', 'Peer', 'communication', 'Communication', 5, 1),
	('00000000-0000-0000-0000-000000000003', 'Peer', 'attitude', 'Attitude', 5, 2),
	('00000000-0000-0000-0000-000000000004', 'Mentor', 'attendance', 'Attendance', 10, 0),
	('00000000-0000-0000-0000-000000000005', 'Mentor', 'task-completion', 'TaskCompletion', 10, 1),
	('00000000-0000-0000-0000-000000000006', 'Mentor', 'learning-initiative', 'LearningInitiative', 10, 2);--> statement-breakpoint

ALTER TABLE "peer_evaluations" ADD COLUMN "scores" jsonb;--> statement-breakpoint
UPDATE "peer_evaluations"
SET "scores" = jsonb_build_array(
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000001', 'criterionName', 'Contribution', 'score', "contribution"),
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000002', 'criterionName', 'Communication', 'score', "communication"),
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000003', 'criterionName', 'Attitude', 'score', "attitude")
);--> statement-breakpoint
ALTER TABLE "peer_evaluations" ALTER COLUMN "scores" SET NOT NULL;--> statement-breakpoint
ALTER TABLE "peer_evaluations" DROP COLUMN "contribution";--> statement-breakpoint
ALTER TABLE "peer_evaluations" DROP COLUMN "communication";--> statement-breakpoint
ALTER TABLE "peer_evaluations" DROP COLUMN "attitude";--> statement-breakpoint

ALTER TABLE "mentor_evaluations" ADD COLUMN "scores" jsonb;--> statement-breakpoint
UPDATE "mentor_evaluations"
SET "scores" = jsonb_build_array(
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000004', 'criterionName', 'Attendance', 'score', "attendance"),
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000005', 'criterionName', 'TaskCompletion', 'score', "task_completion"),
	jsonb_build_object('criterionId', '00000000-0000-0000-0000-000000000006', 'criterionName', 'LearningInitiative', 'score', "learning_initiative")
);--> statement-breakpoint
ALTER TABLE "mentor_evaluations" ALTER COLUMN "scores" SET NOT NULL;--> statement-breakpoint
ALTER TABLE "mentor_evaluations" DROP COLUMN "attendance";--> statement-breakpoint
ALTER TABLE "mentor_evaluations" DROP COLUMN "task_completion";--> statement-breakpoint
ALTER TABLE "mentor_evaluations" DROP COLUMN "learning_initiative";--> statement-breakpoint
