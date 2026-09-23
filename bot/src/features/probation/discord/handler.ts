import { type ChatInputCommandInteraction, MessageFlags } from 'discord.js';
import {
  requireAdminActor,
  requireCandidateActor,
  requireCoreOrAdminActor,
} from '#app/db/authorization.js';
import {
  listActiveDepartments,
  listActiveGenerations,
  listActiveMembers,
  listActiveProbationTeams,
  listProbationCandidates,
} from '#app/db/discord-autocomplete.js';
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
import {
  getCandidateDetail,
  getTeamDetail,
  listCandidateSummaries,
  listTeamSummaries,
  type CandidateStatus,
} from '#app/db/management-read-model.js';
import type { InteractionContext, InteractionHandler } from '#app/discord/interaction-router.js';
import { respondWithAutocomplete } from '#app/discord/autocomplete.js';
import {
  formatCandidateDetail,
  formatCandidateList,
  formatTeamDetail,
  formatTeamList,
} from '#app/discord/management-views.js';
import { replyWithError } from '#app/discord/responses.js';
import { synchronizeDiscordRolesForUser } from '#app/discord/role-synchronization.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';

async function handleCandidateCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'list') {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may list probation candidates.',
      );
      const status = interaction.options.getString('status') as CandidateStatus | null;
      const candidates = await listCandidateSummaries(context.database, {
        ...(status ? { status } : {}),
        ...(interaction.options.getString('team')
          ? { teamId: interaction.options.getString('team', true) }
          : {}),
      });
      await interaction.reply({ content: formatCandidateList(candidates), flags: MessageFlags.Ephemeral });
      return;
    }
    if (subcommand === 'view') {
      const candidateId = interaction.options.getString('candidate', true);
      let canView = false;
      try {
        await requireCoreOrAdminActor(context.database, interaction.user.id);
        canView = true;
      } catch {
        const actor = await requireCandidateActor(context.database, interaction.user.id);
        canView = actor.candidateId === candidateId;
      }
      if (!canView) throw new Error('You may only view your own probation profile.');
      const candidate = await getCandidateDetail(context.database, candidateId);
      if (!candidate) throw new Error('ProbationCandidate was not found.');
      await interaction.reply({ content: formatCandidateDetail(candidate), flags: MessageFlags.Ephemeral });
      return;
    }

    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may manage ProbationCandidates.',
    );
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
        flags: MessageFlags.Ephemeral,
      });
      return;
    }

    let result: ProbationCandidateMutationResult;
    if (subcommand === 'assign-team') {
      result = await assignProbationCandidateTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate', true),
        teamId: interaction.options.getString('team', true),
      });
    } else if (subcommand === 'deactivate') {
      result = await deactivateProbationCandidate(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate', true),
      });
    } else {
      result = await reactivateProbationCandidate(context.database, {
        actorDiscordUserId: interaction.user.id,
        candidateId: interaction.options.getString('candidate', true),
      });
    }

    if (!result.discordUserId) {
      await interaction.reply({
        content: 'ProbationCandidate updated successfully. No linked Discord account required role synchronization.',
        flags: MessageFlags.Ephemeral,
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
        flags: MessageFlags.Ephemeral,
      });
    } catch (error: unknown) {
      const message = getPublicErrorMessage(error, 'Unable to synchronize Discord roles.');
      await interaction.reply({
        content: `ProbationCandidate was updated in the database, but Discord role synchronization failed: ${message}`,
        flags: MessageFlags.Ephemeral,
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
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'list') {
      await requireCoreOrAdminActor(context.database, interaction.user.id);
      await interaction.reply({
        content: formatTeamList(await listTeamSummaries(context.database)),
        flags: MessageFlags.Ephemeral,
      });
      return;
    }
    if (subcommand === 'view') {
      await requireCoreOrAdminActor(context.database, interaction.user.id);
      const team = await getTeamDetail(context.database, interaction.options.getString('team', true));
      if (!team) throw new Error('Probation team was not found.');
      await interaction.reply({ content: formatTeamDetail(team), flags: MessageFlags.Ephemeral });
      return;
    }

    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change reference data.',
    );
    if (subcommand === 'create') {
      await createProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'edit') {
      await editProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team', true),
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'deactivate') {
      await deactivateProbationTeam(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team', true),
      });
    } else {
      await assignProbationTeamMentor(context.database, {
        actorDiscordUserId: interaction.user.id,
        teamId: interaction.options.getString('team', true),
        memberId: interaction.options.getString('member', true),
      });
    }
    await interaction.reply({ content: 'Operation completed.', flags: MessageFlags.Ephemeral });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
}

export const handleProbationInteraction: InteractionHandler = async (interaction, context) => {
  if (interaction.isAutocomplete()) {
    if (interaction.commandName === 'probation-candidate') {
      try {
        const subcommand = interaction.options.getSubcommand();
        const focused = interaction.options.getFocused(true);
        let manager = true;
        let ownCandidateId: string | undefined;
        try {
          await requireCoreOrAdminActor(context.database, interaction.user.id);
        } catch {
          const actor = await requireCandidateActor(context.database, interaction.user.id);
          manager = false;
          ownCandidateId = actor.candidateId;
        }
        if (manager && subcommand !== 'list' && subcommand !== 'view') {
          await requireAdminActor(context.database, interaction.user.id);
        }
        if (focused.name === 'department') {
          if (!manager) {
            await respondWithAutocomplete(interaction, []);
            return true;
          }
          await respondWithAutocomplete(interaction, await listActiveDepartments(
            context.database,
            focused.value,
            subcommand === 'create' ? 'slug' : 'id',
          ));
        } else if (focused.name === 'generation') {
          await respondWithAutocomplete(interaction, await listActiveGenerations(
            context.database,
            focused.value,
            subcommand === 'create' ? 'name' : 'id',
          ));
        } else if (focused.name === 'team') {
          if (!manager) {
            await respondWithAutocomplete(interaction, []);
            return true;
          }
          await respondWithAutocomplete(interaction, await listActiveProbationTeams(context.database, focused.value));
        } else if (focused.name === 'candidate') {
          await respondWithAutocomplete(interaction, await listProbationCandidates(
            context.database,
            focused.value,
            ownCandidateId
              ? { candidateId: ownCandidateId }
            : subcommand === 'reactivate'
              ? { status: 'Inactive' }
              : subcommand === 'assign-team'
                ? { status: 'Active', withoutTeam: true }
                : subcommand === 'deactivate'
                ? { status: 'Active' }
                : {},
          ));
        } else {
          await respondWithAutocomplete(interaction, []);
        }
      } catch {
        await respondWithAutocomplete(interaction, []);
      }
      return true;
    }

    if (interaction.commandName === 'probation-team') {
      try {
        const focused = interaction.options.getFocused(true);
        const subcommand = interaction.options.getSubcommand();
        if (subcommand === 'list') {
          await respondWithAutocomplete(interaction, []);
          return true;
        }
        await requireCoreOrAdminActor(context.database, interaction.user.id);
        if (subcommand !== 'view') {
          await requireAdminActor(context.database, interaction.user.id);
        }
        if (focused.name === 'team') {
          await respondWithAutocomplete(interaction, await listActiveProbationTeams(context.database, focused.value));
        } else if (focused.name === 'member') {
          await respondWithAutocomplete(interaction, await listActiveMembers(context.database, focused.value));
        } else {
          await respondWithAutocomplete(interaction, []);
        }
      } catch {
        await respondWithAutocomplete(interaction, []);
      }
      return true;
    }
    return false;
  }

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
