-- DropIndex
DROP INDEX "departments_slug_key";

-- AlterTable
ALTER TABLE "departments" DROP COLUMN "slug";

-- AlterTable
ALTER TABLE "people" ADD COLUMN "probation_approved" BOOLEAN NOT NULL DEFAULT false;

-- CreateIndex
CREATE UNIQUE INDEX "departments_name_key" ON "departments"("name");
