import { AttachmentBuilder, MessageFlags, type AutocompleteInteraction, type ChatInputCommandInteraction } from 'discord.js';
import {
  requireAdminActor,
  requireCandidateActor,
  requireCoreOrAdminActor,
  requireMentorActor,
} from '#app/db/authorization.js';
import {
  listActiveEvaluationCriteria,
  listEvaluationPeriods,
  listProbationCandidates,
} from '#app/db/discord-autocomplete.js';
import {
  listCriterionSummaries,
  listEvaluationPeriodSummaries,
} from '#app/db/management-read-model.js';
import type { InteractionContext, InteractionHandler } from '#app/discord/interaction-router.js';
import { respondWithAutocomplete } from '#app/discord/autocomplete.js';
import {
  formatCriteriaList,
  formatEvaluationPeriodDetail,
  formatEvaluationPeriodList,
} from '#app/discord/management-views.js';
import { replyWithError } from '#app/discord/responses.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';
import {
  createEvaluationDraft,
  createEvaluationScoresModal,
  createEvaluationNoteModal,
  createEvaluationSelection,
  deleteEvaluationDraft,
  deleteEvaluationSelection,
  findEvaluationDraft,
  findEvaluationSelection,
  getEvaluationDraftPageCriteria,
  renderEvaluationDraft,
  renderEvaluationSelection,
  setEvaluationSelectionCandidate,
  setEvaluationSelectionPeriod,
  setEvaluationDraftNote,
  setEvaluationDraftPage,
  setEvaluationDraftScore,
} from './evaluation-ui.js';
import {
  createEvaluationReportWorkbook,
  createEvaluationReportView,
  findEvaluationReportView,
  renderEvaluationReportView,
  setEvaluationReportPage,
} from './evaluation-report-ui.js';

async function handleCriteriaCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'list') {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may view evaluation criteria.',
      );
      await interaction.reply({
        content: formatCriteriaList(await listCriterionSummaries(context.database)),
        ephemeral: true,
      });
      return;
    }
    const actor = await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change evaluation criteria.',
    );
    if (subcommand === 'add') {
      await context.services.evaluations.addCriterion(actor, {
        kind: interaction.options.getString('kind', true) as 'Peer' | 'Mentor',
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'rename') {
      await context.services.evaluations.renameCriterion(actor, {
        criterionId: interaction.options.getString('criterion', true),
        name: interaction.options.getString('name', true),
      });
    } else {
      await context.services.evaluations.deactivateCriterion(actor, {
        criterionId: interaction.options.getString('criterion', true),
      });
    }
    await interaction.reply({ content: 'Evaluation criterion updated.', flags: MessageFlags.Ephemeral });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to update evaluation criteria.');
  }
}

async function handlePeriodCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'list') {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may view evaluation periods.',
      );
      await interaction.reply({
        content: formatEvaluationPeriodList(await listEvaluationPeriodSummaries(context.database)),
        ephemeral: true,
      });
      return;
    }
    if (subcommand === 'view') {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may view evaluation periods.',
      );
      const periodId = interaction.options.getString('period', true);
      const period = (await listEvaluationPeriodSummaries(context.database))
        .find((item) => item.id === periodId);
      if (!period) throw new Error('Evaluation period was not found.');
      await interaction.reply({ content: formatEvaluationPeriodDetail(period), ephemeral: true });
      return;
    }
    const actor = await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may manage evaluation periods.',
    );
    if (subcommand === 'open') {
      await context.services.evaluations.openPeriod(actor, {
        name: interaction.options.getString('name', true),
      });
    } else {
      await context.services.evaluations.closePeriod(actor, {
        periodId: interaction.options.getString('period', true),
      });
    }
    await interaction.reply({ content: `Evaluation period ${subcommand === 'open' ? 'opened' : 'closed'}.`,flags: MessageFlags.Ephemeral });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to update the evaluation period.');
  }
}

async function handleStartEvaluationCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const subcommand = interaction.options.getSubcommand();
    const kind = subcommand === 'peer' ? 'Peer' : 'Mentor';
    const selection = await createSelectionForActor(context, kind, interaction.user.id);
    await interaction.reply({
      ...renderEvaluationSelection(selection),
      flags: MessageFlags.Ephemeral,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to start the evaluation.');
  }
}

async function createSelectionForActor(
  context: InteractionContext,
  kind: 'Peer' | 'Mentor',
  actorDiscordUserId: string,
  preferredPeriodId?: string,
  excludeCandidateId?: string,
): Promise<ReturnType<typeof createEvaluationSelection>> {
  let candidateOptions;
  if (kind === 'Peer') {
    const actor = await requireCandidateActor(
      context.database,
      actorDiscordUserId,
      'Only an active linked ProbationCandidate may start a Peer evaluation.',
    );
    if (!actor.teamId) {
      throw new Error('You must be assigned to a probation team before starting a Peer evaluation.');
    }
    candidateOptions = await listProbationCandidates(context.database, '', {
      status: 'Active',
      teamId: actor.teamId,
      excludeCandidateId: excludeCandidateId ?? actor.candidateId,
    });
  } else {
    const actor = await requireMentorActor(
      context.database,
      actorDiscordUserId,
      'Only an active linked mentor may start a Mentor evaluation.',
    );
    candidateOptions = await listProbationCandidates(context.database, '', {
      status: 'Active',
      teamIds: actor.teamIds,
      ...(excludeCandidateId ? { excludeCandidateId } : {}),
    });
  }
  const periods = await listEvaluationPeriods(context.database, '', 'Open');
  if (periods.length === 0) throw new Error('No open evaluation period is available.');
  if (candidateOptions.length === 0) throw new Error('No eligible candidate is available for evaluation.');

  return createEvaluationSelection({
    kind,
    actorDiscordUserId,
    periods: periods.map((period) => ({ id: period.value, name: period.name })),
    candidates: candidateOptions.map((candidate) => ({
      id: candidate.value,
      name: candidate.name,
      studentId: candidate.description?.split(' · ')[0] ?? '',
    })),
    ...(preferredPeriodId ? { selectedPeriodId: preferredPeriodId } : {}),
  });
}

async function handleEvaluationReportCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const actor = await requireCoreOrAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Core or Admin may read raw evaluations.',
    );
    const subcommand = interaction.options.getSubcommand();
    const periodId = interaction.options.getString('period', true);
    if (subcommand === 'view') {
      const kind = interaction.options.getString('kind') as 'Peer' | 'Mentor' | null;
      const report = await context.services.evaluations.getRawReport(actor, {
        periodId,
        targetCandidateId: interaction.options.getString('candidate', true),
        ...(kind ? { kind } : {}),
      });
      const view = createEvaluationReportView(interaction.user.id, report);
      await interaction.reply({
        ...renderEvaluationReportView(view),
       flags: MessageFlags.Ephemeral,
      });
      return;
    }

    const report = await context.services.evaluations.getRawReport(actor, { periodId });
    const attachment = new AttachmentBuilder(
      await createEvaluationReportWorkbook(report),
      { name: 'evaluation-report.xlsx' },
    );
    await interaction.reply({
      content: `Exported ${report.evaluations.length} evaluations for ${report.period.name} as an Excel workbook.`,
      files: [attachment],
     flags: MessageFlags.Ephemeral,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to read raw evaluations.');
  }
}

async function handleEvaluationAutocomplete(
  interaction: AutocompleteInteraction,
  context: InteractionContext,
): Promise<boolean> {
  try {
    const focused = interaction.options.getFocused(true);
    if (interaction.commandName === 'evaluation-criteria') {
      await requireAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Admin may change evaluation criteria.',
      );
      await respondWithAutocomplete(
        interaction,
        await listActiveEvaluationCriteria(context.database, focused.value),
      );
      return true;
    }

    if (interaction.commandName === 'evaluation-period') {
      const subcommand = interaction.options.getSubcommand();
      if (subcommand === 'view' || subcommand === 'list') {
        await requireCoreOrAdminActor(
          context.database,
          interaction.user.id,
          'Only a linked active Core or Admin may view evaluation periods.',
        );
      } else {
        await requireAdminActor(
          context.database,
          interaction.user.id,
          'Only a linked active Admin may manage evaluation periods.',
        );
      }
      await respondWithAutocomplete(
        interaction,
        await listEvaluationPeriods(context.database, focused.value, 'Open'),
      );
      return true;
    }

    if (interaction.commandName === 'evaluation') {
      const subcommand = interaction.options.getSubcommand();
      if (subcommand === 'peer') {
        const actor = await requireCandidateActor(context.database, interaction.user.id);
        if (focused.name === 'target-candidate') {
          await respondWithAutocomplete(
            interaction,
            actor.teamId
              ? await listProbationCandidates(context.database, focused.value, {
                status: 'Active',
                teamId: actor.teamId,
                excludeCandidateId: actor.candidateId,
              })
              : [],
          );
        } else {
          await respondWithAutocomplete(
            interaction,
            await listEvaluationPeriods(context.database, focused.value, 'Open'),
          );
        }
        return true;
      }

      const actor = await requireMentorActor(context.database, interaction.user.id);
      if (focused.name === 'target-candidate') {
        await respondWithAutocomplete(
          interaction,
          await listProbationCandidates(context.database, focused.value, {
            status: 'Active',
            teamIds: actor.teamIds,
          }),
        );
      } else {
        await respondWithAutocomplete(
          interaction,
          await listEvaluationPeriods(context.database, focused.value, 'Open'),
        );
      }
      return true;
    }

    if (interaction.commandName === 'evaluation-report') {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may read raw evaluations.',
      );
      if (focused.name === 'candidate') {
        await respondWithAutocomplete(
          interaction,
          await listProbationCandidates(context.database, focused.value),
        );
      } else {
        await respondWithAutocomplete(
          interaction,
          await listEvaluationPeriods(context.database, focused.value),
        );
      }
      return true;
    }
  } catch {
    await respondWithAutocomplete(interaction, []);
    return true;
  }

  return false;
}

export const handleEvaluationInteraction: InteractionHandler = async (interaction, context) => {
  if (interaction.isAutocomplete()) {
    return handleEvaluationAutocomplete(interaction, context);
  }

  if (interaction.isChatInputCommand()) {
    if (interaction.commandName === 'evaluation-criteria') {
      await handleCriteriaCommand(interaction, context);
      return true;
    }
    if (interaction.commandName === 'evaluation-period') {
      await handlePeriodCommand(interaction, context);
      return true;
    }
    if (interaction.commandName === 'evaluation') {
      await handleStartEvaluationCommand(interaction, context);
      return true;
    }
    if (interaction.commandName === 'evaluation-report') {
      await handleEvaluationReportCommand(interaction, context);
      return true;
    }
    return false;
  }

  if (interaction.isButton() && interaction.customId.startsWith('evaluation-report:page:')) {
    const [, , viewId, direction] = interaction.customId.split(':');
    const view = findEvaluationReportView(viewId ?? '');
    if (!view) {
      await interaction.reply({ content: 'This evaluation report has expired. Run the command again.',flags: MessageFlags.Ephemeral });
      return true;
    }
    if (view.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation report belongs to another Discord user.',flags: MessageFlags.Ephemeral });
      return true;
    }

    try {
      await requireCoreOrAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Core or Admin may read raw evaluations.',
      );
      setEvaluationReportPage(view, view.page + (direction === 'next' ? 1 : -1));
      await interaction.update(renderEvaluationReportView(view));
    } catch (error: unknown) {
      await replyWithError(interaction, error, 'Unable to read raw evaluations.');
    }
    return true;
  }

  if (interaction.isStringSelectMenu() && interaction.customId.startsWith('evaluation:select-')) {
    const [, action, selectionId] = interaction.customId.split(':');
    const selection = findEvaluationSelection(selectionId ?? '');
    if (!selection) {
      await interaction.reply({ content: 'This evaluation setup has expired. Start the command again.', flags: MessageFlags.Ephemeral });
      return true;
    }
    if (selection.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation setup belongs to another Discord user.', flags: MessageFlags.Ephemeral });
      return true;
    }
    try {
      if (action === 'select-period') {
        setEvaluationSelectionPeriod(selection, interaction.values[0] ?? '');
      } else {
        setEvaluationSelectionCandidate(selection, interaction.values[0] ?? '');
      }
      await interaction.update(renderEvaluationSelection(selection));
    } catch (error: unknown) {
      await replyWithError(interaction, error, 'Unable to update the evaluation selection.');
    }
    return true;
  }

  if (interaction.isStringSelectMenu() && interaction.customId.startsWith('evaluation:score:')) {
    const [, , draftId, criterionId] = interaction.customId.split(':');
    const draft = findEvaluationDraft(draftId ?? '');
    if (!draft || !criterionId) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.',flags: MessageFlags.Ephemeral });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.',flags: MessageFlags.Ephemeral });
      return true;
    }

    try {
      setEvaluationDraftScore(draft, criterionId, Number.parseInt(interaction.values[0] ?? '', 10));
      await interaction.update(renderEvaluationDraft(draft));
    } catch (error: unknown) {
      await replyWithError(interaction, error, 'Unable to save this score.');
    }
    return true;
  }

  if (interaction.isButton() && interaction.customId.startsWith('evaluation:')) {
    const parts = interaction.customId.split(':');
    const action = parts[1];
    if (action === 'start' || action === 'cancel-selection') {
      const selection = findEvaluationSelection(parts[2] ?? '');
      if (!selection) {
        await interaction.reply({ content: 'This evaluation setup has expired. Start the command again.', flags: MessageFlags.Ephemeral });
        return true;
      }
      if (selection.actorDiscordUserId !== interaction.user.id) {
        await interaction.reply({ content: 'This evaluation setup belongs to another Discord user.', flags: MessageFlags.Ephemeral });
        return true;
      }
      if (action === 'cancel-selection') {
        deleteEvaluationSelection(selection.id);
        await interaction.update({ content: 'Evaluation cancelled.', components: [] });
        return true;
      }
      if (!selection.selectedPeriodId || !selection.selectedCandidateId) {
        await interaction.reply({ content: 'Choose both an evaluation period and a candidate first.', flags: MessageFlags.Ephemeral });
        return true;
      }
      try {
        const submissionContext = await context.services.evaluations.getSubmissionContext({
          kind: selection.kind,
          periodId: selection.selectedPeriodId,
          targetCandidateId: selection.selectedCandidateId,
          actorDiscordUserId: interaction.user.id,
        });
        deleteEvaluationSelection(selection.id);
        await interaction.update(renderEvaluationDraft(createEvaluationDraft(submissionContext)));
      } catch (error: unknown) {
        await replyWithError(interaction, error, 'Unable to start the evaluation.');
      }
      return true;
    }
    const draft = findEvaluationDraft(parts[2] ?? '');
    if (!draft) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.',flags: MessageFlags.Ephemeral });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.',flags: MessageFlags.Ephemeral });
      return true;
    }

    if (action === 'page') {
      setEvaluationDraftPage(draft, draft.page + (parts[3] === 'next' ? 1 : -1));
      await interaction.update(renderEvaluationDraft(draft));
      return true;
    }
    if (action === 'note') {
      await interaction.showModal(createEvaluationNoteModal(draft));
      return true;
    }
    if (action === 'scores') {
      await interaction.showModal(createEvaluationScoresModal(draft));
      return true;
    }
    if (action === 'submit') {
      await interaction.deferUpdate();
      try {
        const input = {
          periodId: draft.periodId,
          targetCandidateId: draft.targetCandidateId,
          actorDiscordUserId: draft.actorDiscordUserId,
          scores: [...draft.scores.entries()].map(([criterionId, score]) => ({ criterionId, score })),
          note: draft.note,
        };
        if (draft.kind === 'Peer') {
          await context.services.evaluations.submitPeerEvaluation(input);
        } else {
          await context.services.evaluations.submitMentorEvaluation(input);
        }
        let nextSelection;
        try {
          nextSelection = await createSelectionForActor(
            context,
            draft.kind,
            draft.actorDiscordUserId,
            draft.periodId,
            draft.targetCandidateId,
          );
        } catch {
          nextSelection = undefined;
        }
        deleteEvaluationDraft(draft.id);
        if (nextSelection) {
          await interaction.editReply({
            content: `${draft.kind} evaluation submitted successfully. Choose another candidate below.`,
            components: renderEvaluationSelection(nextSelection).components,
          });
        } else {
          await interaction.editReply({
            content: `${draft.kind} evaluation submitted successfully.`,
            components: [],
          });
        }
      } catch (error: unknown) {
        const message = getPublicErrorMessage(error, 'Unable to submit the evaluation.');
        await interaction.editReply({ content: message, components: renderEvaluationDraft(draft).components });
      }
      return true;
    }
  }

  if (interaction.isModalSubmit() && interaction.customId.startsWith('evaluation:scores:')) {
    const draftId = interaction.customId.split(':')[2] ?? '';
    const draft = findEvaluationDraft(draftId);
    if (!draft) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.', flags: MessageFlags.Ephemeral });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.', flags: MessageFlags.Ephemeral });
      return true;
    }

    try {
      for (const criterion of getEvaluationDraftPageCriteria(draft)) {
        const rawScore = interaction.fields.getTextInputValue(`score:${criterion.id}`).trim();
        const score = Number.parseInt(rawScore, 10);
        if (!/^\d+$/.test(rawScore) || !Number.isInteger(score)
          || score < criterion.minScore || score > criterion.maxScore) {
          throw new Error(`${criterion.name} must be a whole number between ${criterion.minScore} and ${criterion.maxScore}.`);
        }
        setEvaluationDraftScore(draft, criterion.id, score);
      }
      if (interaction.isFromMessage()) {
        await interaction.update(renderEvaluationDraft(draft));
      } else {
        await interaction.reply({ content: 'Scores saved. Return to the evaluation form to continue.', flags: MessageFlags.Ephemeral });
      }
    } catch (error: unknown) {
      await replyWithError(interaction, error, 'Unable to save evaluation scores.');
    }
    return true;
  }

  if (interaction.isModalSubmit() && interaction.customId.startsWith('evaluation:note:')) {
    const draftId = interaction.customId.split(':')[2] ?? '';
    const draft = findEvaluationDraft(draftId);
    if (!draft) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.',flags: MessageFlags.Ephemeral });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.',flags: MessageFlags.Ephemeral });
      return true;
    }

    setEvaluationDraftNote(draft, interaction.fields.getTextInputValue('note'));
    if (interaction.isFromMessage()) {
      await interaction.update(renderEvaluationDraft(draft));
    } else {
      await interaction.reply({ content: 'Evaluation note saved. Return to the evaluation form to submit.',flags: MessageFlags.Ephemeral });
    }
    return true;
  }

  return false;
};
