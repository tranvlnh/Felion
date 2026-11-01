-- CreateSchema
CREATE SCHEMA IF NOT EXISTS "public";

-- CreateEnum
CREATE TYPE "PersonKind" AS ENUM ('Candidate', 'Member');

-- CreateEnum
CREATE TYPE "PersonStatus" AS ENUM ('Active', 'Inactive');

-- CreateEnum
CREATE TYPE "WebPermission" AS ENUM ('ViewPeople', 'ManagePeople', 'AssignRoles', 'KickMembers');

-- CreateTable
CREATE TABLE "departments" (
    "id" UUID NOT NULL,
    "name" VARCHAR(100) NOT NULL,
    "slug" VARCHAR(100) NOT NULL,
    "active" BOOLEAN NOT NULL DEFAULT true,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "departments_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "people" (
    "id" UUID NOT NULL,
    "kind" "PersonKind" NOT NULL,
    "status" "PersonStatus" NOT NULL DEFAULT 'Active',
    "student_id" VARCHAR(32) NOT NULL,
    "full_name" VARCHAR(200) NOT NULL,
    "club_email" VARCHAR(320),
    "discord_user_id" VARCHAR(32),
    "is_admin" BOOLEAN NOT NULL DEFAULT false,
    "department_id" UUID NOT NULL,
    "generation" INTEGER NOT NULL,
    "team_name" VARCHAR(100),
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "people_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "person_roles" (
    "person_id" UUID NOT NULL,
    "discord_role_id" VARCHAR(32) NOT NULL,

    CONSTRAINT "person_roles_pkey" PRIMARY KEY ("person_id","discord_role_id")
);

-- CreateTable
CREATE TABLE "role_permissions" (
    "discord_role_id" VARCHAR(32) NOT NULL,
    "web_permissions" "WebPermission"[],

    CONSTRAINT "role_permissions_pkey" PRIMARY KEY ("discord_role_id")
);

-- CreateTable
CREATE TABLE "settings" (
    "id" INTEGER NOT NULL DEFAULT 1,
    "probation_role_id" VARCHAR(32),
    "member_role_id" VARCHAR(32),

    CONSTRAINT "settings_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "audit_logs" (
    "id" UUID NOT NULL,
    "actor_id" UUID,
    "action" VARCHAR(100) NOT NULL,
    "entity_type" VARCHAR(100) NOT NULL,
    "entity_id" VARCHAR(100),
    "metadata" JSONB,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "audit_logs_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "departments_slug_key" ON "departments"("slug");

-- CreateIndex
CREATE UNIQUE INDEX "people_student_id_key" ON "people"("student_id");

-- CreateIndex
CREATE UNIQUE INDEX "people_club_email_key" ON "people"("club_email");

-- CreateIndex
CREATE UNIQUE INDEX "people_discord_user_id_key" ON "people"("discord_user_id");

-- CreateIndex
CREATE INDEX "people_kind_team_name_idx" ON "people"("kind", "team_name");

-- CreateIndex
CREATE INDEX "person_roles_discord_role_id_idx" ON "person_roles"("discord_role_id");

-- CreateIndex
CREATE INDEX "audit_logs_created_at_idx" ON "audit_logs"("created_at");

-- AddForeignKey
ALTER TABLE "people" ADD CONSTRAINT "people_department_id_fkey" FOREIGN KEY ("department_id") REFERENCES "departments"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "person_roles" ADD CONSTRAINT "person_roles_person_id_fkey" FOREIGN KEY ("person_id") REFERENCES "people"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "audit_logs" ADD CONSTRAINT "audit_logs_actor_id_fkey" FOREIGN KEY ("actor_id") REFERENCES "people"("id") ON DELETE SET NULL ON UPDATE CASCADE;
