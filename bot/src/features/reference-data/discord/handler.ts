import { MessageFlags } from 'discord.js';
import { requireAdminActor } from '#app/db/authorization.js';
import { listActiveDepartments, listActiveGenerations } from '#app/db/discord-autocomplete.js';
import {
  listDepartmentSummaries,
  listGenerationSummaries,
} from '#app/db/management-read-model.js';
import {
  createDepartment,
  createGeneration,
  deactivateDepartment,
  deactivateGeneration,
  editDepartment,
  editGeneration,
} from '#app/db/reference-management.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { respondWithAutocomplete } from '#app/discord/autocomplete.js';
import { replyWithError } from '#app/discord/responses.js';
import { formatReferenceList } from '#app/discord/management-views.js';

export const handleReferenceDataInteraction: InteractionHandler = async (interaction, context) => {
  if (interaction.isAutocomplete()) {
    if (interaction.commandName !== 'department' && interaction.commandName !== 'generation') {
      return false;
    }
    try {
      await requireAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Admin may change reference data.',
      );
      const focused = interaction.options.getFocused(true);
      const items = interaction.commandName === 'department'
        ? await listActiveDepartments(context.database, focused.value)
        : await listActiveGenerations(context.database, focused.value);
      await respondWithAutocomplete(interaction, items);
    } catch {
      await respondWithAutocomplete(interaction, []);
    }
    return true;
  }

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
    if (subcommand === 'list') {
      const content = interaction.commandName === 'department'
        ? formatReferenceList('Departments', await listDepartmentSummaries(context.database))
        : formatReferenceList('Generations', await listGenerationSummaries(context.database));
      await interaction.reply({ content, ephemeral: true });
      return true;
    }
    if (interaction.commandName === 'department' && subcommand === 'create') {
      await createDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
        slug: interaction.options.getString('slug', true),
      });
    } else if (interaction.commandName === 'department' && subcommand === 'edit') {
      await editDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        departmentId: interaction.options.getString('department', true),
        name: interaction.options.getString('name', true),
        slug: interaction.options.getString('slug', true),
      });
    } else if (interaction.commandName === 'department') {
      await deactivateDepartment(context.database, {
        actorDiscordUserId: interaction.user.id,
        departmentId: interaction.options.getString('department', true),
      });
    } else if (subcommand === 'create') {
      await createGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'edit') {
      await editGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        generationId: interaction.options.getString('generation', true),
        name: interaction.options.getString('name', true),
      });
    } else {
      await deactivateGeneration(context.database, {
        actorDiscordUserId: interaction.user.id,
        generationId: interaction.options.getString('generation', true),
      });
    }
    await interaction.reply({ content: 'Operation completed.', flags: MessageFlags.Ephemeral });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
  return true;
};
