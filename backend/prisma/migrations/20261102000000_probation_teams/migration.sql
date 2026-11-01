-- CreateTable
CREATE TABLE "probation_teams" (
    "id" UUID NOT NULL,
    "name" VARCHAR(100) NOT NULL,
    "discord_role_id" VARCHAR(32),
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "probation_teams_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "probation_team_mentors" (
    "team_id" UUID NOT NULL,
    "person_id" UUID NOT NULL,

    CONSTRAINT "probation_team_mentors_pkey" PRIMARY KEY ("team_id","person_id")
);

-- AlterTable
ALTER TABLE "people" ADD COLUMN "team_id" UUID;

-- Keep the current round: every distinct team_name becomes a team (no Discord role yet).
INSERT INTO "probation_teams" ("id", "name")
SELECT gen_random_uuid(), "team_name" FROM "people" WHERE "team_name" IS NOT NULL GROUP BY "team_name";

UPDATE "people" p SET "team_id" = t."id"
FROM "probation_teams" t
WHERE p."team_name" = t."name" AND p."kind" = 'Candidate';

INSERT INTO "probation_team_mentors" ("team_id", "person_id")
SELECT t."id", p."id" FROM "people" p JOIN "probation_teams" t ON p."team_name" = t."name"
WHERE p."kind" = 'Member';

-- DropIndex
DROP INDEX "people_kind_team_name_idx";

-- AlterTable
ALTER TABLE "people" DROP COLUMN "team_name";

-- CreateIndex
CREATE UNIQUE INDEX "probation_teams_name_key" ON "probation_teams"("name");

-- CreateIndex
CREATE UNIQUE INDEX "probation_teams_discord_role_id_key" ON "probation_teams"("discord_role_id");

-- CreateIndex
CREATE INDEX "probation_team_mentors_person_id_idx" ON "probation_team_mentors"("person_id");

-- CreateIndex
CREATE INDEX "people_team_id_idx" ON "people"("team_id");

-- AddForeignKey
ALTER TABLE "people" ADD CONSTRAINT "people_team_id_fkey" FOREIGN KEY ("team_id") REFERENCES "probation_teams"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "probation_team_mentors" ADD CONSTRAINT "probation_team_mentors_team_id_fkey" FOREIGN KEY ("team_id") REFERENCES "probation_teams"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "probation_team_mentors" ADD CONSTRAINT "probation_team_mentors_person_id_fkey" FOREIGN KEY ("person_id") REFERENCES "people"("id") ON DELETE CASCADE ON UPDATE CASCADE;
