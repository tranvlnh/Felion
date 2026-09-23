import { randomUUID } from 'node:crypto';
import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
} from 'discord.js';
import type { RawEvaluationReport } from '../application/evaluation-service.js';

const reportLifetimeMs = 15 * 60 * 1000;
const reportPageLength = 1_800;

export type EvaluationReportView = {
  id: string;
  actorDiscordUserId: string;
  page: number;
  pages: string[];
};

const reportViews = new Map<string, EvaluationReportView>();

function splitReportContent(content: string): string[] {
  const pages: string[] = [];
  let remaining = content;
  while (remaining.length > reportPageLength) {
    const candidateBreak = remaining.lastIndexOf('\n', reportPageLength);
    const breakAt = candidateBreak > 0 ? candidateBreak : reportPageLength;
    pages.push(remaining.slice(0, breakAt));
    remaining = remaining.slice(breakAt).replace(/^\n/, '');
  }
  if (remaining.length > 0) {
    pages.push(remaining);
  }
  return pages;
}

function formatReport(report: RawEvaluationReport): string {
  const header = [
    `Raw evaluation report — ${report.period.name}`,
    `Period ID: ${report.period.id}`,
    `Status: ${report.period.status}`,
    report.target
      ? `Target: ${report.target.fullName} (${report.target.id})`
      : 'Target: all candidates',
  ];
  if (report.evaluations.length === 0) {
    return `${header.join('\n')}\n\nNo evaluations matched this report.`;
  }

  const evaluations = report.evaluations.map((evaluation, index) => [
    `Evaluation ${index + 1}/${report.evaluations.length}`,
    `ID: ${evaluation.id}`,
    `Kind: ${evaluation.kind}`,
    `Submitted (UTC): ${evaluation.submittedAt.toISOString()}`,
    `Evaluator: ${evaluation.evaluatorName} (${evaluation.evaluatorId})`,
    `Target: ${evaluation.targetName} (${evaluation.targetCandidateId})`,
    'Scores:',
    ...evaluation.scores.map((score) =>
      `- ${score.criterionName} [${score.criterionId}]: ${score.score}`),
    `Note: ${evaluation.note ?? '(none)'}`,
  ].join('\n'));

  return `${header.join('\n')}\n\n${evaluations.join('\n\n')}`;
}

export function createEvaluationReportView(
  actorDiscordUserId: string,
  report: RawEvaluationReport,
): EvaluationReportView {
  const view: EvaluationReportView = {
    id: randomUUID(),
    actorDiscordUserId,
    page: 0,
    pages: splitReportContent(formatReport(report)),
  };
  reportViews.set(view.id, view);
  const timeout = setTimeout(() => reportViews.delete(view.id), reportLifetimeMs);
  timeout.unref();
  return view;
}

export function findEvaluationReportView(id: string): EvaluationReportView | undefined {
  return reportViews.get(id);
}

export function setEvaluationReportPage(view: EvaluationReportView, page: number): void {
  view.page = Math.min(Math.max(page, 0), view.pages.length - 1);
}

export function renderEvaluationReportView(view: EvaluationReportView): {
  content: string;
  components: ActionRowBuilder<ButtonBuilder>[];
} {
  const navigation = new ActionRowBuilder<ButtonBuilder>().addComponents(
    new ButtonBuilder()
      .setCustomId(`evaluation-report:page:${view.id}:previous`)
      .setLabel('Previous')
      .setStyle(ButtonStyle.Secondary)
      .setDisabled(view.page === 0),
    new ButtonBuilder()
      .setCustomId(`evaluation-report:page:${view.id}:next`)
      .setLabel('Next')
      .setStyle(ButtonStyle.Secondary)
      .setDisabled(view.page >= view.pages.length - 1),
  );

  return {
    content: `${view.pages[view.page] ?? 'This report is empty.'}\n\nPage ${view.page + 1}/${view.pages.length}`,
    components: [navigation],
  };
}

function escapeCsvCell(value: string | number): string {
  const raw = String(value);
  const text = typeof value === 'string' && /^\s*[=+\-@]/.test(raw)
    ? `'${raw}`
    : raw;
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

export function createEvaluationReportCsv(report: RawEvaluationReport): string {
  const headers = [
    'period_id',
    'period_name',
    'evaluation_id',
    'kind',
    'submitted_at_utc',
    'target_candidate_id',
    'target_name',
    'evaluator_id',
    'evaluator_name',
    'criterion_id',
    'criterion_name',
    'score',
    'note',
  ];
  const rows = report.evaluations.flatMap((evaluation) =>
    evaluation.scores.map((score) => [
      report.period.id,
      report.period.name,
      evaluation.id,
      evaluation.kind,
      evaluation.submittedAt.toISOString(),
      evaluation.targetCandidateId,
      evaluation.targetName,
      evaluation.evaluatorId,
      evaluation.evaluatorName,
      score.criterionId,
      score.criterionName,
      score.score,
      evaluation.note ?? '',
    ]));

  return `\uFEFF${[headers, ...rows]
    .map((row) => row.map(escapeCsvCell).join(','))
    .join('\r\n')}\r\n`;
}
