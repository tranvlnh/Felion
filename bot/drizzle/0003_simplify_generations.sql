DROP INDEX IF EXISTS "generations_code_unique";--> statement-breakpoint
ALTER TABLE "generations" DROP COLUMN IF EXISTS "code";--> statement-breakpoint
ALTER TABLE "generations" DROP COLUMN IF EXISTS "created_at";--> statement-breakpoint
ALTER TABLE "generations" DROP COLUMN IF EXISTS "updated_at";--> statement-breakpoint
CREATE UNIQUE INDEX IF NOT EXISTS "generations_name_unique" ON "generations" USING btree ("name");
