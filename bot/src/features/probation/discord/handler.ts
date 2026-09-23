import type { ChatInputCommandInteraction } from 'discord.js';
import { requireAdminActor } from '#app/db/authorization.js';
import {
  assignProbationCandidateTeam,
  createActiveProbationCandidate,
  deactivateProbationCandidate,
  reactivateProbationCandidate,
  type ProbationCandidateMutationResult,
} from '#app/db/probation-candidate-management.js';
import {
  assignProbationTeamMentor,
  createProbationTeam,
  deactivateProbationTeam,
  editProbationTeam,
} from '#app/db/probation-team-management.js';
import type { InteractionContext, InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';
import { synchronizeDiscordRolesForUser } from '#app/discord/role-synchronization.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';

async function handleCandidateCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may manage ProbationCandidates.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'create') {
      await createActiveProbationCandidate(context.database, {
        actorDiscordUserId: interaction.user.id,
        studentId: interaction.options.getString('student-id', true),
        fullName: interaction.options.getString('full-name', true),
        department: interaction.options.getString('department', true),
        generation: interaction.options.getString('generation', true),
      });
      await interaction.reply({
        content: 'ProbationCandidate created successfully. A team can be assigned later.',
        ephemeral: true,
      });
      return;
    }

    let result: ProbationCandidateMutationResult;
    if (subcommand === 'assign-team') {
      result = await assignProbationCandidateTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate-id', true),
        teamId: interaction.options.getString('team-id', true),
      });
    } else if (subcommand === 'deactivate') {
      result = await deactivateProbationCandidate(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate-id', true),
      });
    } else {
      result = await reactivateProbationCandidate(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate-id', true),
      });
    }

    if (!result.discordUserId) {
      await interaction.reply({
        content: 'ProbationCandidate updated successfully. No linked Discord account required role synchronization.',
        ephemeral: true,
      });
      return;
    }

    try {
      const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
      const roleSync = await synchronizeDiscordRolesForUser(
        context.database,
        guild,
        result.discordUserId,
        interaction.user.id,
      );
      await interaction.reply({
        content: `ProbationCandidate updated and roles synchronized: ${roleSync.addedRoleIds.length} added, ${roleSync.removedRoleIds.length} removed.`,
        ephemeral: true,
      });
    } catch (error: unknown) {
      const message = getPublicErrorMessage(error, 'Unable to synchronize Discord roles.');
      await interaction.reply({
        content: `ProbationCandidate was updated in the database, but Discord role synchronization failed: ${message}`,
        ephemeral: true,
      });
    }
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to manage ProbationCandidate.');
  }
}

async function handleTeamCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change reference data.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'create') {
      await createProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'edit') {
      await editProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team-id', true),
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'deactivate') {
      await deactivateProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team-id', true),
      });
    } else {
      await assignProbationTeamMentor(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team-id', true),
        memberId: interaction.options.getString('member-id', true),
      });
    }
    await interaction.reply({ content: 'Operation completed.', ephemeral: true });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
}

export const handleProbationInteraction: InteractionHandler = async (interaction, context) => {
  if (!interaction.isChatInputCommand()) {
    return false;
  }
  if (interaction.commandName === 'probation-candidate') {
    await handleCandidateCommand(interaction, context);
    return true;
  }
  if (interaction.commandName === 'probation-team') {
    await handleTeamCommand(interaction, context);
    return true;
  }
  return false;
};
