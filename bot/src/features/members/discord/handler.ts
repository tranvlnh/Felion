import { requireAdminActor } from '#app/db/authorization.js';
import { bootstrapAdmin } from '#app/db/bootstrap.js';
import { createRegularMember } from '#app/db/member-management.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';

export const handleMemberInteraction: InteractionHandler = async (interaction, context) => {
  if (!interaction.isChatInputCommand()) {
    return false;
  }

  if (interaction.commandName === 'bootstrap-admin') {
    try {
      await bootstrapAdmin(context.database, {
        actorDiscordUserId: interaction.user.id,
        studentId: interaction.options.getString('student-id', true),
        fullName: interaction.options.getString('full-name', true),
        clubEmail: interaction.options.getString('club-email', true),
        generationName: interaction.options.getString('generation-name', true),
      });
      await interaction.reply({ content: 'Initial Admin created. Publish the verification message to link this account.', ephemeral: true });
    } catch (error: unknown) {
      await replyWithError(interaction, error, 'Unable to bootstrap the initial Admin.');
    }
    return true;
  }

  if (interaction.commandName !== 'member') {
    return false;
  }

  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may create Members.',
    );
    await createRegularMember(context.database, {
      actorDiscordUserId: interaction.user.id,
      studentId: interaction.options.getString('student-id', true),
      fullName: interaction.options.getString('full-name', true),
      clubEmail: interaction.options.getString('club-email', true),
      department: interaction.options.getString('department', true),
      generation: interaction.options.getString('generation', true),
    });
    await interaction.reply({ content: 'Member created successfully.', ephemeral: true });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to create Member.');
  }
  return true;
};
