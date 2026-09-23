import { randomUUID } from 'node:crypto';
import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
} from 'discord.js';
import ExcelJS from 'exceljs';
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

type ReportCellValue = string | number | Date | null;

function safeSpreadsheetText(value: string): string {
  return /^\s*[=+\-@]/.test(value) ? `'${value}` : value;
}

function getCriterionColumns(report: RawEvaluationReport): readonly { key: string; header: string }[] {
  const namesByKey = new Map<string, { kind: string; name: string }>();
  for (const evaluation of report.evaluations) {
    for (const score of evaluation.scores) {
      namesByKey.set(`${evaluation.kind}:${score.criterionName}`, {
        kind: evaluation.kind,
        name: score.criterionName,
      });
    }
  }

  const nameCounts = new Map<string, number>();
  for (const criterion of namesByKey.values()) {
    nameCounts.set(criterion.name, (nameCounts.get(criterion.name) ?? 0) + 1);
  }

  return [...namesByKey.entries()].map(([key, criterion]) => ({
    key,
    header: (nameCounts.get(criterion.name) ?? 0) > 1
      ? `${criterion.kind}: ${criterion.name}`
      : criterion.name,
  }));
}

function formatReportWorksheet(
  worksheet: ExcelJS.Worksheet,
  headers: readonly string[],
  rows: ReadonlyArray<ReadonlyArray<ReportCellValue>>,
  widths: readonly number[],
): void {
  worksheet.views = [{ state: 'frozen', ySplit: 1 }];
  worksheet.columns = headers.map((header, index) => ({
    header,
    key: `column${index}`,
    width: widths[index] ?? 18,
  }));
  for (const row of rows) {
    worksheet.addRow([...row]);
  }
  worksheet.autoFilter = {
    from: { row: 1, column: 1 },
    to: { row: Math.max(1, rows.length + 1), column: headers.length },
  };
  worksheet.getRow(1).height = 24;
  worksheet.getRow(1).eachCell((cell) => {
    cell.font = { name: 'Arial', size: 10, bold: true, color: { argb: 'FFFFFFFF' } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1F4E78' } };
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
  });
  worksheet.eachRow((row, rowNumber) => {
    if (rowNumber === 1) return;
    row.alignment = { vertical: 'middle', wrapText: true };
    row.eachCell((cell) => {
      cell.font = { name: 'Arial', size: 10, color: { argb: 'FF1F1F1F' } };
    });
  });
}

function formatRawScoresGroups(
  worksheet: ExcelJS.Worksheet,
  groups: readonly { startRow: number; endRow: number }[],
): void {
  const mergedColumns = [1, 2, 3, 4, 5, 6, 9];
  for (const group of groups) {
    if (group.endRow <= group.startRow) continue;
    for (const column of mergedColumns) {
      worksheet.mergeCells(group.startRow, column, group.endRow, column);
    }
  }

  for (const column of [1, 2, 3, 4, 5, 6, 8, 9]) {
    worksheet.getColumn(column).alignment = {
      horizontal: 'center',
      vertical: 'middle',
      wrapText: true,
    };
  }
  worksheet.getColumn(7).alignment = { horizontal: 'left', vertical: 'middle', wrapText: true };
  worksheet.getColumn(8).numFmt = '0';
  for (const group of groups) {
    const bottomRow = worksheet.getRow(group.endRow);
    bottomRow.eachCell((cell) => {
      cell.border = {
        ...cell.border,
        bottom: { style: 'thin', color: { argb: 'FFB7C9D6' } },
      };
    });
  }
}

export async function createEvaluationReportWorkbook(report: RawEvaluationReport): Promise<Buffer> {
  const workbook = new ExcelJS.Workbook();
  workbook.creator = 'Felion';
  workbook.created = new Date();
  workbook.modified = new Date();

  const criterionColumns = getCriterionColumns(report);
  const summaryHeaders = [
    'Evaluation period',
    'Period status',
    'Evaluation type',
    'Submitted at (UTC)',
    'Candidate',
    'Evaluator',
    ...criterionColumns.map((criterion) => criterion.header),
    'Total score',
    'Average score',
    'Note',
  ];
  const summaryRows = report.evaluations.map((evaluation) => {
    const scoreByKey = new Map(
      evaluation.scores.map((score) => [`${evaluation.kind}:${score.criterionName}`, score.score]),
    );
    const total = evaluation.scores.reduce((sum, score) => sum + score.score, 0);
    return [
      safeSpreadsheetText(report.period.name),
      report.period.status,
      evaluation.kind,
      evaluation.submittedAt,
      safeSpreadsheetText(evaluation.targetName),
      safeSpreadsheetText(evaluation.evaluatorName),
      ...criterionColumns.map((criterion) => scoreByKey.get(criterion.key) ?? null),
      evaluation.scores.length > 0 ? total : null,
      evaluation.scores.length > 0 ? total / evaluation.scores.length : null,
      safeSpreadsheetText(evaluation.note ?? ''),
    ] satisfies ReportCellValue[];
  });
  const summary = workbook.addWorksheet('Summary');
  formatReportWorksheet(summary, summaryHeaders, summaryRows, [24, 14, 16, 22, 28, 28, ...criterionColumns.map(() => 18), 14, 14, 40]);
  summary.getColumn(4).numFmt = 'yyyy-mm-dd hh:mm';
  summary.getColumn(summaryHeaders.length - 1).alignment = { vertical: 'top', wrapText: true };
  summary.getColumn(summaryHeaders.length - 2).numFmt = '0.00';

  const rawHeaders = [
    'Evaluation period',
    'Period status',
    'Evaluation type',
    'Submitted at (UTC)',
    'Candidate',
    'Evaluator',
    'Criterion',
    'Score',
    'Note',
  ];
  const rawRows = report.evaluations.flatMap((evaluation) =>
    evaluation.scores.map((score) => [
      safeSpreadsheetText(report.period.name),
      report.period.status,
      evaluation.kind,
      evaluation.submittedAt,
      safeSpreadsheetText(evaluation.targetName),
      safeSpreadsheetText(evaluation.evaluatorName),
      safeSpreadsheetText(score.criterionName),
      score.score,
      safeSpreadsheetText(evaluation.note ?? ''),
    ] satisfies ReportCellValue[]));
  const raw = workbook.addWorksheet('Raw Scores');
  formatReportWorksheet(raw, rawHeaders, rawRows, [24, 18, 18, 22, 28, 28, 32, 12, 40]);
  raw.getColumn(4).numFmt = 'yyyy-mm-dd hh:mm';
  const rawGroups: { startRow: number; endRow: number }[] = [];
  let rawRow = 2;
  for (const evaluation of report.evaluations) {
    const endRow = rawRow + evaluation.scores.length - 1;
    if (evaluation.scores.length > 0) {
      rawGroups.push({ startRow: rawRow, endRow });
      rawRow = endRow + 1;
    }
  }
  formatRawScoresGroups(raw, rawGroups);

  return Buffer.from(await workbook.xlsx.writeBuffer());
}
