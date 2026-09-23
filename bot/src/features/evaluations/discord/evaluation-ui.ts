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
import type { EvaluationSubmissionContext } from '../application/evaluation-service.js';

const criteriaPerPage = 4;
const draftLifetimeMs = 15 * 60 * 1000;

export type EvaluationDraft = EvaluationSubmissionContext & {
  id: string;
  page: number;
  scores: Map<string, number>;
  note: string | null;
};

const drafts = new Map<string, EvaluationDraft>();

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

export function renderEvaluationDraft(draft: EvaluationDraft) {
  const pageCount = Math.max(1, Math.ceil(draft.criteria.length / criteriaPerPage));
  const pageCriteria = draft.criteria.slice(
    draft.page * criteriaPerPage,
    (draft.page + 1) * criteriaPerPage,
  );
  const rows = pageCriteria.map((criterion) => new ActionRowBuilder<StringSelectMenuBuilder>()
    .addComponents(new StringSelectMenuBuilder()
      .setCustomId(`evaluation:score:${draft.id}:${criterion.id}`)
      .setPlaceholder(`${criterion.name} (${criterion.minScore}-${criterion.maxScore})`)
      .setMinValues(1)
      .setMaxValues(1)
      .addOptions(Array.from(
        { length: criterion.maxScore - criterion.minScore + 1 },
        (_, index) => {
          const score = criterion.minScore + index;
          return {
            label: String(score),
            value: String(score),
            default: draft.scores.get(criterion.id) === score,
          };
        },
      ))));
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
      + 'Score every criterion, optionally add one note, then submit.',
    components: [...rows, navigation],
  };
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
