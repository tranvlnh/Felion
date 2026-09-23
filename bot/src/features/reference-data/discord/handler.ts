import { requireAdminActor } from '#app/db/authorization.js';
import {
  createDepartment,
  createGeneration,
  deactivateDepartment,
  deactivateGeneration,
  editDepartment,
  editGeneration,
} from '#app/db/reference-management.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';

export const handleReferenceDataInteraction: InteractionHandler = async (interaction, context) => {
  if (!interaction.isChatInputCommand()
    || (interaction.commandName !== 'department' && interaction.commandName !== 'generation')) {
    return false;
  }

  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change reference data.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (interaction.commandName === 'department' && subcommand === 'create') {
      await createDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
        slug: interaction.options.getString('slug', true),
      });
    } else if (interaction.commandName === 'department' && subcommand === 'edit') {
      await editDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        departmentId: interaction.options.getString('department-id', true),
        name: interaction.options.getString('name', true),
        slug: interaction.options.getString('slug', true),
      });
    } else if (interaction.commandName === 'department') {
      await deactivateDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        departmentId: interaction.options.getString('department-id', true),
      });
    } else if (subcommand === 'create') {
      await createGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'edit') {
      await editGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        generationId: interaction.options.getString('generation-id', true),
        name: interaction.options.getString('name', true),
      });
    } else {
      await deactivateGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        generationId: interaction.options.getString('generation-id', true),
      });
    }
    await interaction.reply({ content: 'Operation completed.', ephemeral: true });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
  return true;
};
