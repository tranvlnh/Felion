import { randomUUID } from 'node:crypto';
import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  ModalBuilder,
  StringSelectMenuBuilder,
  TextInputBuilder,
  TextInputStyle,
} from 'discord.js';
import type { EvaluationKind, EvaluationCriterion } from '#app/domain/evaluation.js';
import type { EvaluationSubmissionContext } from '../application/evaluation-service.js';

const criteriaPerPage = 5;
const draftLifetimeMs = 15 * 60 * 1000;

export type EvaluationDraft = EvaluationSubmissionContext & {
  id: string;
  page: number;
  scores: Map<string, number>;
  note: string | null;
};

export type EvaluationSelection = {
  id: string;
  kind: EvaluationKind;
  actorDiscordUserId: string;
  periods: readonly { id: string; name: string }[];
  candidates: readonly { id: string; name: string; studentId: string }[];
  selectedPeriodId: string | null;
  selectedCandidateId: string | null;
};

const drafts = new Map<string, EvaluationDraft>();
const selections = new Map<string, EvaluationSelection>();

export function createEvaluationDraft(context: EvaluationSubmissionContext): EvaluationDraft {
  const draft: EvaluationDraft = {
    ...context,
    id: randomUUID(),
    page: 0,
    scores: new Map(),
    note: null,
  };
  drafts.set(draft.id, draft);
  const timeout = setTimeout(() => drafts.delete(draft.id), draftLifetimeMs);
  timeout.unref();
  return draft;
}

export function findEvaluationDraft(id: string): EvaluationDraft | undefined {
  return drafts.get(id);
}

export function deleteEvaluationDraft(id: string): void {
  drafts.delete(id);
}

export function createEvaluationSelection(input: Omit<EvaluationSelection, 'id' | 'selectedPeriodId' | 'selectedCandidateId'> & {
  selectedPeriodId?: string | null;
}): EvaluationSelection {
  const selection: EvaluationSelection = {
    ...input,
    id: randomUUID(),
    selectedPeriodId: input.selectedPeriodId ?? (input.periods.length === 1 ? input.periods[0]?.id ?? null : null),
    selectedCandidateId: null,
  };
  selections.set(selection.id, selection);
  const timeout = setTimeout(() => selections.delete(selection.id), draftLifetimeMs);
  timeout.unref();
  return selection;
}

export function findEvaluationSelection(id: string): EvaluationSelection | undefined {
  return selections.get(id);
}

export function deleteEvaluationSelection(id: string): void {
  selections.delete(id);
}

export function setEvaluationSelectionPeriod(selection: EvaluationSelection, periodId: string): void {
  if (!selection.periods.some((period) => period.id === periodId)) {
    throw new Error('This evaluation period is no longer available.');
  }
  selection.selectedPeriodId = periodId;
}

export function setEvaluationSelectionCandidate(selection: EvaluationSelection, candidateId: string): void {
  if (!selection.candidates.some((candidate) => candidate.id === candidateId)) {
    throw new Error('This evaluation candidate is no longer available.');
  }
  selection.selectedCandidateId = candidateId;
}

export function renderEvaluationSelection(selection: EvaluationSelection) {
  const components: ActionRowBuilder<StringSelectMenuBuilder | ButtonBuilder>[] = [];
  if (selection.periods.length > 1) {
    components.push(new ActionRowBuilder<StringSelectMenuBuilder>().addComponents(
      new StringSelectMenuBuilder()
        .setCustomId(`evaluation:select-period:${selection.id}`)
        .setPlaceholder('Choose an open evaluation period')
        .addOptions(selection.periods.slice(0, 25).map((period) => ({
          label: period.name.slice(0, 100),
          value: period.id,
          default: selection.selectedPeriodId === period.id,
        }))),
    ));
  }
  components.push(new ActionRowBuilder<StringSelectMenuBuilder>().addComponents(
    new StringSelectMenuBuilder()
      .setCustomId(`evaluation:select-candidate:${selection.id}`)
      .setPlaceholder(`Choose a ${selection.kind === 'Peer' ? 'peer' : 'candidate'} to evaluate`)
      .addOptions(selection.candidates.slice(0, 25).map((candidate) => ({
        label: candidate.name.slice(0, 100),
        description: candidate.studentId.slice(0, 100),
        value: candidate.id,
        default: selection.selectedCandidateId === candidate.id,
      }))),
  ));
  components.push(new ActionRowBuilder<ButtonBuilder>().addComponents(
    new ButtonBuilder()
      .setCustomId(`evaluation:start:${selection.id}`)
      .setLabel('Open evaluation form')
      .setStyle(ButtonStyle.Primary)
      .setDisabled(!selection.selectedPeriodId || !selection.selectedCandidateId),
    new ButtonBuilder()
      .setCustomId(`evaluation:cancel-selection:${selection.id}`)
      .setLabel('Cancel')
      .setStyle(ButtonStyle.Secondary),
  ));

  const periodName = selection.periods.find((period) => period.id === selection.selectedPeriodId)?.name;
  return {
    content: `${selection.kind} evaluation setup.\n`
      + `${periodName ? `Period: **${periodName}**\n` : ''}`
      + 'Choose a target, then open the modal to enter scores.',
    components,
  };
}

export function setEvaluationDraftScore(
  draft: EvaluationDraft,
  criterionId: string,
  score: number,
): void {
  if (!draft.criteria.some((criterion) => criterion.id === criterionId)) {
    throw new Error('This evaluation criterion is no longer part of the draft.');
  }
  draft.scores.set(criterionId, score);
}

export function setEvaluationDraftNote(draft: EvaluationDraft, note: string): void {
  draft.note = note.trim() || null;
}

export function setEvaluationDraftPage(draft: EvaluationDraft, page: number): void {
  const pageCount = Math.max(1, Math.ceil(draft.criteria.length / criteriaPerPage));
  draft.page = Math.min(Math.max(page, 0), pageCount - 1);
}

export function getEvaluationDraftPageCriteria(draft: EvaluationDraft): readonly EvaluationCriterion[] {
  const pageCount = Math.max(1, Math.ceil(draft.criteria.length / criteriaPerPage));
  const page = Math.min(Math.max(draft.page, 0), pageCount - 1);
  return draft.criteria.slice(page * criteriaPerPage, (page + 1) * criteriaPerPage);
}

export function renderEvaluationDraft(draft: EvaluationDraft) {
  const pageCount = Math.max(1, Math.ceil(draft.criteria.length / criteriaPerPage));
  const pageCriteria = getEvaluationDraftPageCriteria(draft);
  const scoreSummary = pageCriteria.map((criterion) =>
    `• ${criterion.name}: ${draft.scores.get(criterion.id)?.toString() ?? 'Chưa nhập'} (${criterion.minScore}-${criterion.maxScore})`);
  const navigation = new ActionRowBuilder<ButtonBuilder>().addComponents(
    new ButtonBuilder()
      .setCustomId(`evaluation:page:${draft.id}:previous`)
      .setLabel('Previous')
      .setStyle(ButtonStyle.Secondary)
      .setDisabled(draft.page === 0),
    new ButtonBuilder()
      .setCustomId(`evaluation:page:${draft.id}:next`)
      .setLabel('Next')
      .setStyle(ButtonStyle.Secondary)
      .setDisabled(draft.page >= pageCount - 1),
    new ButtonBuilder()
      .setCustomId(`evaluation:scores:${draft.id}`)
      .setLabel('Enter scores')
      .setStyle(ButtonStyle.Primary),
    new ButtonBuilder()
      .setCustomId(`evaluation:note:${draft.id}`)
      .setLabel(draft.note ? 'Edit note' : 'Add note')
      .setStyle(ButtonStyle.Secondary),
    new ButtonBuilder()
      .setCustomId(`evaluation:submit:${draft.id}`)
      .setLabel('Submit evaluation')
      .setStyle(ButtonStyle.Primary)
      .setDisabled(draft.scores.size !== draft.criteria.length),
  );

  return {
    content: `${draft.kind} evaluation for **${draft.targetName}** (page ${draft.page + 1}/${pageCount}).\n`
      + 'Use the modal to enter every score on this page.\n'
      + scoreSummary.join('\n')
      + (draft.note ? `\nNote: ${draft.note}` : ''),
    components: [navigation],
  };
}

export function createEvaluationScoresModal(draft: EvaluationDraft): ModalBuilder {
  const inputs = getEvaluationDraftPageCriteria(draft).map((criterion) => {
    const input = new TextInputBuilder()
      .setCustomId(`score:${criterion.id}`)
      .setLabel(criterion.name.slice(0, 45))
      .setPlaceholder(`Enter ${criterion.minScore}-${criterion.maxScore}`)
      .setStyle(TextInputStyle.Short)
      .setRequired(true)
      .setMaxLength(2);
    const currentScore = draft.scores.get(criterion.id);
    if (currentScore !== undefined) input.setValue(String(currentScore));
    return new ActionRowBuilder<TextInputBuilder>().addComponents(input);
  });

  return new ModalBuilder()
    .setCustomId(`evaluation:scores:${draft.id}`)
    .setTitle(`${draft.kind} scores ${draft.page + 1}`)
    .addComponents(inputs);
}

export function createEvaluationNoteModal(draft: EvaluationDraft): ModalBuilder {
  const note = new TextInputBuilder()
    .setCustomId('note')
    .setLabel('Optional note')
    .setStyle(TextInputStyle.Paragraph)
    .setRequired(false)
    .setMaxLength(2000);
  if (draft.note) {
    note.setValue(draft.note);
  }

  return new ModalBuilder()
    .setCustomId(`evaluation:note:${draft.id}`)
    .setTitle('Evaluation note')
    .addComponents(new ActionRowBuilder<TextInputBuilder>().addComponents(note));
}
