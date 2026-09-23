import { and, asc, eq, ilike, inArray, isNull, ne, or } from 'drizzle-orm';
import type { Database } from './client.js';
import {
  departments,
  evaluationCriteria,
  evaluationPeriods,
  generations,
  members,
  probationCandidates,
  probationTeams,
} from './schema.js';

export type AutocompleteItem = {
  value: string;
  name: string;
  description?: string;
};

const MAX_AUTOCOMPLETE_RESULTS = 25;

function searchFilter(search: string, columns: Parameters<typeof ilike>[0][]): ReturnType<typeof or> | undefined {
  const normalized = search.trim();
  if (!normalized) {
    return undefined;
  }

  return or(...columns.map((column) => ilike(column, `%${normalized}%`)));
}

export async function listActiveDepartments(
  database: Database,
  search: string,
  valueMode: 'id' | 'slug' = 'id',
): Promise<AutocompleteItem[]> {
  const filter = searchFilter(search, [departments.name, departments.slug]);
  const rows = await database.db
    .select({ id: departments.id, name: departments.name, slug: departments.slug })
    .from(departments)
    .where(filter ? and(eq(departments.active, true), filter) : eq(departments.active, true))
    .orderBy(asc(departments.name))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({
    value: valueMode === 'id' ? row.id : row.slug,
    name: row.name,
    description: row.slug,
  }));
}

export async function listActiveGenerations(
  database: Database,
  search: string,
  valueMode: 'id' | 'name' = 'id',
): Promise<AutocompleteItem[]> {
  const filter = searchFilter(search, [generations.name]);
  const rows = await database.db
    .select({ id: generations.id, name: generations.name })
    .from(generations)
    .where(filter ? and(eq(generations.active, true), filter) : eq(generations.active, true))
    .orderBy(asc(generations.name))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({ value: valueMode === 'id' ? row.id : row.name, name: row.name }));
}

export async function listActiveProbationTeams(
  database: Database,
  search: string,
): Promise<AutocompleteItem[]> {
  const filter = searchFilter(search, [probationTeams.name]);
  const rows = await database.db
    .select({ id: probationTeams.id, name: probationTeams.name })
    .from(probationTeams)
    .where(filter ? and(eq(probationTeams.active, true), filter) : eq(probationTeams.active, true))
    .orderBy(asc(probationTeams.name))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({ value: row.id, name: row.name }));
}

export async function listActiveMembers(
  database: Database,
  search: string,
): Promise<AutocompleteItem[]> {
  const filter = searchFilter(search, [members.fullName, members.studentId]);
  const rows = await database.db
    .select({ id: members.id, fullName: members.fullName, studentId: members.studentId })
    .from(members)
    .where(filter ? and(eq(members.status, 'Active'), filter) : eq(members.status, 'Active'))
    .orderBy(asc(members.fullName), asc(members.studentId))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({
    value: row.id,
    name: row.fullName,
    description: row.studentId,
  }));
}

export type CandidateAutocompleteFilter = {
  candidateId?: string;
  status?: 'Active' | 'Inactive' | 'Passed' | 'Failed';
  teamId?: string;
  teamIds?: readonly string[];
  excludeCandidateId?: string;
  withoutTeam?: boolean;
};

export async function listProbationCandidates(
  database: Database,
  search: string,
  filterInput: CandidateAutocompleteFilter = {},
): Promise<AutocompleteItem[]> {
  if (filterInput.teamIds && filterInput.teamIds.length === 0) {
    return [];
  }

  const filters = [];
  if (filterInput.status) {
    filters.push(eq(probationCandidates.status, filterInput.status));
  }
  if (filterInput.candidateId) {
    filters.push(eq(probationCandidates.id, filterInput.candidateId));
  }
  if (filterInput.teamId) {
    filters.push(eq(probationCandidates.teamId, filterInput.teamId));
  }
  if (filterInput.teamIds) {
    filters.push(inArray(probationCandidates.teamId, [...filterInput.teamIds]));
  }
  if (filterInput.withoutTeam) {
    filters.push(isNull(probationCandidates.teamId));
  }
  if (filterInput.excludeCandidateId) {
    filters.push(ne(probationCandidates.id, filterInput.excludeCandidateId));
  }

  const textFilter = searchFilter(search, [probationCandidates.fullName, probationCandidates.studentId]);
  if (textFilter) {
    filters.push(textFilter);
  }

  const rows = await database.db
    .select({
      id: probationCandidates.id,
      fullName: probationCandidates.fullName,
      studentId: probationCandidates.studentId,
      status: probationCandidates.status,
    })
    .from(probationCandidates)
    .where(filters.length > 0 ? and(...filters) : undefined)
    .orderBy(asc(probationCandidates.fullName), asc(probationCandidates.studentId))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({
    value: row.id,
    name: row.fullName,
    description: `${row.studentId} · ${row.status}`,
  }));
}

export async function listEvaluationPeriods(
  database: Database,
  search: string,
  status?: 'Open' | 'Closed',
): Promise<AutocompleteItem[]> {
  const filters = [];
  if (status) {
    filters.push(eq(evaluationPeriods.status, status));
  }
  const textFilter = searchFilter(search, [evaluationPeriods.name]);
  if (textFilter) {
    filters.push(textFilter);
  }

  const rows = await database.db
    .select({ id: evaluationPeriods.id, name: evaluationPeriods.name, status: evaluationPeriods.status })
    .from(evaluationPeriods)
    .where(filters.length > 0 ? and(...filters) : undefined)
    .orderBy(asc(evaluationPeriods.openedAt), asc(evaluationPeriods.name))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({
    value: row.id,
    name: row.name,
    description: row.status,
  }));
}

export async function listActiveEvaluationCriteria(
  database: Database,
  search: string,
): Promise<AutocompleteItem[]> {
  const textFilter = searchFilter(search, [evaluationCriteria.name, evaluationCriteria.key]);
  const rows = await database.db
    .select({
      id: evaluationCriteria.id,
      kind: evaluationCriteria.kind,
      name: evaluationCriteria.name,
      key: evaluationCriteria.key,
    })
    .from(evaluationCriteria)
    .where(textFilter ? and(eq(evaluationCriteria.active, true), textFilter) : eq(evaluationCriteria.active, true))
    .orderBy(asc(evaluationCriteria.kind), asc(evaluationCriteria.sortOrder), asc(evaluationCriteria.name))
    .limit(MAX_AUTOCOMPLETE_RESULTS);

  return rows.map((row) => ({
    value: row.id,
    name: `${row.kind}: ${row.name}`,
    description: row.key,
  }));
}

export async function listRoleMappingKeys(
  database: Database,
  kind: string | null,
  search: string,
): Promise<AutocompleteItem[]> {
  if (kind === 'Position') {
    return filterStaticItems([
      { value: 'Admin', name: 'Admin' },
      { value: 'Core', name: 'Core' },
      { value: 'Member', name: 'Member' },
    ], search);
  }
  if (kind === 'Probation') {
    return filterStaticItems([{ value: 'Active', name: 'Active' }], search);
  }
  if (kind === 'Department') {
    return listActiveDepartments(database, search);
  }
  if (kind === 'Generation') {
    return listActiveGenerations(database, search);
  }
  if (kind === 'ProbationTeam') {
    return listActiveProbationTeams(database, search);
  }
  return [];
}

function filterStaticItems(items: AutocompleteItem[], search: string): AutocompleteItem[] {
  const normalized = search.trim().toLowerCase();
  return normalized
    ? items.filter((item) => item.name.toLowerCase().includes(normalized))
    : items;
}
