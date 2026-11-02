import type { FastifyInstance } from 'fastify';
import { z, ZodError } from 'zod';
import { listDepartments } from '#app/db/departments.js';
import { findExistingStudentIds, importPeople, listPeople, type PersonInput } from '#app/db/people.js';
import { parseCsv, toCsv } from '#app/domain/csv.js';
import { normalizeClubEmail, normalizeFullName, normalizeStudentId } from '#app/domain/member.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';
import { assertCan } from '../permissions.js';
import type { WebContext } from '../server.js';
import { assertAdminForFields, createBody, listQuery } from './people.js';

export const importColumns = ['studentId', 'fullName', 'department', 'generation', 'phone', 'personalEmail', 'birthDate', 'facebookUrl', 'clubEmail'] as const;
const requiredColumns = ['studentId', 'fullName', 'department', 'generation'];
const maxImportRows = 500;

export type ImportRowError = { row: number; error: string };

function rowError(error: unknown): string {
  if (error instanceof ZodError) {
    const issue = error.issues[0];
    return `${issue?.path.join('.') || 'row'}: ${issue?.message ?? 'invalid'}`;
  }
  return getPublicErrorMessage(error, 'Invalid row.');
}

// Row numbers are spreadsheet rows: the header is row 1.
export function parseImportRows(
  csv: string,
  kind: PersonInput['kind'],
  departments: readonly { id: string; name: string; active: boolean }[],
): { inputs: { row: number; input: PersonInput }[]; errors: ImportRowError[] } {
  const [header, ...rows] = parseCsv(csv);
  if (!header) throw new AppError('VALIDATION', 'The CSV file is empty.');
  const columns = header.map((column) => column.trim());
  const unknown = columns.filter((column) => !(importColumns as readonly string[]).includes(column));
  if (unknown.length > 0) throw new AppError('VALIDATION', `Unknown columns: ${unknown.join(', ')}. Allowed: ${importColumns.join(', ')}.`);
  if (new Set(columns).size !== columns.length) throw new AppError('VALIDATION', 'A column appears twice in the header.');
  const missing = requiredColumns.filter((column) => !columns.includes(column));
  if (missing.length > 0) throw new AppError('VALIDATION', `Missing columns: ${missing.join(', ')}.`);
  if (rows.length === 0) throw new AppError('VALIDATION', 'The CSV file has no data rows.');
  if (rows.length > maxImportRows) throw new AppError('VALIDATION', `At most ${maxImportRows} rows per file.`);

  const departmentIds = new Map(departments.filter((d) => d.active).map((d) => [d.name.toLowerCase(), d.id]));
  const seen = new Map<string, number>();
  const inputs: { row: number; input: PersonInput }[] = [];
  const errors: ImportRowError[] = [];

  rows.forEach((cells, index) => {
    const row = index + 2;
    try {
      if (cells.length > columns.length) throw new AppError('VALIDATION', 'More cells than header columns.');
      const record = Object.fromEntries(
        columns.map((column, i) => [column, cells[i]?.trim() ?? ''] as const).filter(([, value]) => value !== ''),
      );
      const { department, generation, ...rest } = record;
      const departmentId = department ? departmentIds.get(department.toLowerCase()) : undefined;
      if (!departmentId) throw new AppError('VALIDATION', `department: "${department ?? ''}" does not exist or is inactive.`);

      const input = createBody.parse({ ...rest, kind, departmentId, generation: generation === undefined ? undefined : Number(generation) });
      // Checked here so a bad row is reported by number instead of failing the whole transaction.
      const studentId = normalizeStudentId(input.studentId);
      normalizeFullName(input.fullName);
      if (input.clubEmail) normalizeClubEmail(input.clubEmail);

      const firstRow = seen.get(studentId);
      if (firstRow) throw new AppError('CONFLICT', `studentId ${studentId} is also on row ${firstRow}.`);
      seen.set(studentId, row);
      inputs.push({ row, input: { ...input, studentId } });
    } catch (error) {
      errors.push({ row, error: rowError(error) });
    }
  });
  return { inputs, errors };
}

const isoDate = (date: Date | null) => date?.toISOString().slice(0, 10) ?? '';

export function registerPeopleCsvRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;

  app.post('/people/import', async (request) => {
    const { kind, csv, dryRun } = z.object({
      kind: z.enum(['Candidate', 'Member']),
      csv: z.string().max(1_000_000),
      dryRun: z.boolean(),
    }).strict().parse(request.body);
    assertCan(request.actor, 'ManagePeople');

    const { inputs, errors } = parseImportRows(csv, kind, await listDepartments(db));
    for (const { input } of inputs) assertAdminForFields(request.actor, input);
    const existing = await findExistingStudentIds(db, inputs.map(({ input }) => input.studentId));
    const fresh = inputs.filter(({ row, input }) => {
      if (!existing.has(input.studentId)) return true;
      errors.push({ row, error: `studentId ${input.studentId} already exists.` });
      return false;
    });
    if (errors.length > 0 || dryRun) {
      return { valid: fresh.length, errors: errors.sort((a, b) => a.row - b.row) };
    }
    return { created: await importPeople(db, fresh.map(({ input }) => input), request.actor.id) };
  });

  app.get('/people/export.csv', async (request, reply) => {
    assertCan(request.actor, 'ViewPeople');
    // shortcut: caps at 10k rows, stream if the club ever gets that big
    const { items } = await listPeople(db, { ...listQuery.parse(request.query), page: 1, pageSize: 10_000 });
    const rows = items.map((p) => [
      p.kind, p.status, p.studentId, p.fullName, p.department.name, p.generation, p.team?.name, p.clubEmail,
      p.phone, p.personalEmail, isoDate(p.birthDate), p.facebookUrl, p.discordUserId,
    ]);
    const header = ['kind', 'status', 'studentId', 'fullName', 'department', 'generation', 'team', 'clubEmail',
      'phone', 'personalEmail', 'birthDate', 'facebookUrl', 'discordUserId'];
    return reply
      .header('content-type', 'text/csv; charset=utf-8')
      .header('content-disposition', 'attachment; filename="felion-people.csv"')
      // BOM so Excel reads the Vietnamese text as UTF-8.
      .send(`﻿${toCsv([header, ...rows])}`);
  });
}
