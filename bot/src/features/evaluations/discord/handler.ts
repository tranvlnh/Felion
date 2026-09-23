import { AttachmentBuilder, type ChatInputCommandInteraction } from 'discord.js';
import { requireAdminActor, requireCoreOrAdminActor } from '#app/db/authorization.js';
import type { InteractionContext, InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';
import {
  createEvaluationDraft,
  createEvaluationNoteModal,
  deleteEvaluationDraft,
  findEvaluationDraft,
  renderEvaluationDraft,
  setEvaluationDraftNote,
  setEvaluationDraftPage,
  setEvaluationDraftScore,
} from './evaluation-ui.js';
import {
  createEvaluationReportCsv,
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
    const actor = await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change evaluation criteria.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'add') {
      await context.services.evaluations.addCriterion(actor, {
        kind: interaction.options.getString('kind', true) as 'Peer' | 'Mentor',
        name: interaction.options.getString('name', true),
      });
    } else if (subcommand === 'rename') {
      await context.services.evaluations.renameCriterion(actor, {
        criterionId: interaction.options.getString('criterion-id', true),
        name: interaction.options.getString('name', true),
      });
    } else {
      await context.services.evaluations.deactivateCriterion(actor, {
        criterionId: interaction.options.getString('criterion-id', true),
      });
    }
    await interaction.reply({ content: 'Evaluation criterion updated.', ephemeral: true });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to update evaluation criteria.');
  }
}

async function handlePeriodCommand(
  interaction: ChatInputCommandInteraction,
  context: InteractionContext,
): Promise<void> {
  try {
    const actor = await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may manage evaluation periods.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'open') {
      await context.services.evaluations.openPeriod(actor, {
        name: interaction.options.getString('name', true),
      });
    } else {
      await context.services.evaluations.closePeriod(actor, {
        periodId: interaction.options.getString('period-id', true),
      });
    }
    await interaction.reply({ content: `Evaluation period ${subcommand === 'open' ? 'opened' : 'closed'}.`, ephemeral: true });
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
    const submissionContext = await context.services.evaluations.getSubmissionContext({
      kind: subcommand === 'peer' ? 'Peer' : 'Mentor',
      periodId: interaction.options.getString('period-id', true),
      targetCandidateId: interaction.options.getString('target-candidate-id', true),
      actorDiscordUserId: interaction.user.id,
    });
    await interaction.reply({
      ...renderEvaluationDraft(createEvaluationDraft(submissionContext)),
      ephemeral: true,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to start the evaluation.');
  }
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
    const periodId = interaction.options.getString('period-id', true);
    if (subcommand === 'view') {
      const kind = interaction.options.getString('kind') as 'Peer' | 'Mentor' | null;
      const report = await context.services.evaluations.getRawReport(actor, {
        periodId,
        targetCandidateId: interaction.options.getString('candidate-id', true),
        ...(kind ? { kind } : {}),
      });
      const view = createEvaluationReportView(interaction.user.id, report);
      await interaction.reply({
        ...renderEvaluationReportView(view),
        ephemeral: true,
      });
      return;
    }

    const report = await context.services.evaluations.getRawReport(actor, { periodId });
    const attachment = new AttachmentBuilder(
      Buffer.from(createEvaluationReportCsv(report), 'utf8'),
      { name: `evaluation-report-${report.period.id}.csv` },
    );
    await interaction.reply({
      content: `Exported ${report.evaluations.length} raw evaluations for ${report.period.name}.`,
      files: [attachment],
      ephemeral: true,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to read raw evaluations.');
  }
}

export const handleEvaluationInteraction: InteractionHandler = async (interaction, context) => {
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
      await interaction.reply({ content: 'This evaluation report has expired. Run the command again.', ephemeral: true });
      return true;
    }
    if (view.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation report belongs to another Discord user.', ephemeral: true });
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

  if (interaction.isStringSelectMenu() && interaction.customId.startsWith('evaluation:score:')) {
    const [, , draftId, criterionId] = interaction.customId.split(':');
    const draft = findEvaluationDraft(draftId ?? '');
    if (!draft || !criterionId) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.', ephemeral: true });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.', ephemeral: true });
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
    const draft = findEvaluationDraft(parts[2] ?? '');
    if (!draft) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.', ephemeral: true });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.', ephemeral: true });
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
        deleteEvaluationDraft(draft.id);
        await interaction.editReply({
          content: `${draft.kind} evaluation submitted successfully.`,
          components: [],
        });
      } catch (error: unknown) {
        const message = getPublicErrorMessage(error, 'Unable to submit the evaluation.');
        await interaction.editReply({ content: message, components: renderEvaluationDraft(draft).components });
      }
      return true;
    }
  }

  if (interaction.isModalSubmit() && interaction.customId.startsWith('evaluation:note:')) {
    const draftId = interaction.customId.split(':')[2] ?? '';
    const draft = findEvaluationDraft(draftId);
    if (!draft) {
      await interaction.reply({ content: 'This evaluation form has expired. Start it again.', ephemeral: true });
      return true;
    }
    if (draft.actorDiscordUserId !== interaction.user.id) {
      await interaction.reply({ content: 'This evaluation form belongs to another Discord user.', ephemeral: true });
      return true;
    }

    setEvaluationDraftNote(draft, interaction.fields.getTextInputValue('note'));
    if (interaction.isFromMessage()) {
      await interaction.update(renderEvaluationDraft(draft));
    } else {
      await interaction.reply({ content: 'Evaluation note saved. Return to the evaluation form to submit.', ephemeral: true });
    }
    return true;
  }

  return false;
};
