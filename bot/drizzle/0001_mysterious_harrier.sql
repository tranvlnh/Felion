CREATE TABLE "identity_registry" (
	"student_id" varchar(32) PRIMARY KEY NOT NULL,
	"subject_type" "discord_subject_type" NOT NULL,
	"subject_id" uuid NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
