-- AlterTable
ALTER TABLE "settings" ADD COLUMN     "verification_button_label" VARCHAR(80),
ADD COLUMN     "verification_channel_id" VARCHAR(32),
ADD COLUMN     "verification_description" VARCHAR(4096),
ADD COLUMN     "verification_message_id" VARCHAR(32),
ADD COLUMN     "verification_title" VARCHAR(256);
