import { normalizeClubEmail, normalizeFullName, normalizeStudentId } from '#app/domain/member.js';
import type { Prisma } from '#app/generated/prisma/client.js';
import type { PersonKind, PersonStatus } from '#app/generated/prisma/enums.js';
import { AppError } from '#app/errors.js';
import { assertKeepsAnAdmin } from '#app/api/permissions.js';
import { writeAudit } from './audit.js';
import { rethrowUniqueViolation, type Db, type Tx } from './client.js';
import { getSettings } from './settings.js';

export type PersonFilter = {
  kind?: PersonKind | undefined;
  status?: PersonStatus | undefined;
  departmentId?: string | undefined;
  generation?: number | undefined;
  search?: string | undefined;
  page: number;
  pageSize: number;
};

export type PersonPatch = {
  studentId?: string | undefined;
  fullName?: string | undefined;
  departmentId?: string | undefined;
  generation?: number | undefined;
  status?: PersonStatus | undefined;
  clubEmail?: string | null | undefined;
  phone?: string | null | undefined;
  personalEmail?: string | null | undefined;
  // YYYY-MM-DD
  birthDate?: string | null | undefined;
  facebookUrl?: string | null | undefined;
  discordUserId?: string | null | undefined;
  isAdmin?: boolean | undefined;
};

export type PersonInput = PersonPatch & {
  kind: PersonKind;
  studentId: string;
  fullName: string;
  departmentId: string;
  generation: number;
};

const uniqueMessage = 'Another person already uses this StudentId, club email or Discord account.';

const personSelect = {
  id: true,
  kind: true,
  status: true,
  studentId: true,
  fullName: true,
  clubEmail: true,
  phone: true,
  personalEmail: true,
  birthDate: true,
  facebookUrl: true,
  discordUserId: true,
  isAdmin: true,
  generation: true,
  team: { select: { id: true, name: true, discordRoleId: true } },
  probationApproved: true,
  createdAt: true,
  updatedAt: true,
  department: { select: { id: true, name: true } },
} satisfies Prisma.PersonSelect;

function normalizePatch(patch: PersonPatch): Prisma.PersonUncheckedUpdateInput {
  const data: Prisma.PersonUncheckedUpdateInput = {};
  if (patch.studentId !== undefined) data.studentId = normalizeStudentId(patch.studentId);
  if (patch.fullName !== undefined) data.fullName = normalizeFullName(patch.fullName);
  if (patch.departmentId !== undefined) data.departmentId = patch.departmentId;
  if (patch.generation !== undefined) data.generation = patch.generation;
  if (patch.status !== undefined) data.status = patch.status;
  if (patch.clubEmail !== undefined) data.clubEmail = patch.clubEmail === null ? null : normalizeClubEmail(patch.clubEmail);
  if (patch.phone !== undefined) data.phone = patch.phone;
  if (patch.personalEmail !== undefined) data.personalEmail = patch.personalEmail;
  if (patch.birthDate !== undefined) data.birthDate = patch.birthDate === null ? null : new Date(patch.birthDate);
  if (patch.facebookUrl !== undefined) data.facebookUrl = patch.facebookUrl;
  if (patch.discordUserId !== undefined) data.discordUserId = patch.discordUserId;
  if (patch.isAdmin !== undefined) data.isAdmin = patch.isAdmin;
  return data;
}

export async function listPeople(db: Db, filter: PersonFilter) {
  const search = filter.search?.trim();
  const where: Prisma.PersonWhereInput = {
    ...(filter.kind ? { kind: filter.kind } : {}),
    ...(filter.status ? { status: filter.status } : {}),
    ...(filter.departmentId ? { departmentId: filter.departmentId } : {}),
    ...(filter.generation ? { generation: filter.generation } : {}),
    ...(search
      ? {
        OR: [
          { fullName: { contains: search, mode: 'insensitive' } },
          { studentId: { contains: search, mode: 'insensitive' } },
          { clubEmail: { contains: search, mode: 'insensitive' } },
          { discordUserId: search },
        ],
      }
      : {}),
  };
  const [items, total] = await Promise.all([
    db.person.findMany({
      where,
      select: personSelect,
      orderBy: [{ generation: 'desc' }, { fullName: 'asc' }],
      skip: (filter.page - 1) * filter.pageSize,
      take: filter.pageSize,
    }),
    db.person.count({ where }),
  ]);
  return { items, total };
}

export async function getPerson(db: Db | Tx, id: string) {
  const person = await db.person.findUnique({
    where: { id },
    select: {
      ...personSelect,
      roles: { select: { discordRoleId: true } },
      mentoring: { select: { team: { select: { id: true, name: true } } }, orderBy: { team: { name: 'asc' } } },
    },
  });
  if (!person) {
    throw new AppError('NOT_FOUND', 'Person not found.');
  }
  const { mentoring, ...rest } = person;
  return { ...rest, roles: person.roles.map((role) => role.discordRoleId).sort(), mentorTeams: mentoring.map((row) => row.team) };
}

export async function findPersonByDiscordUserId(db: Db, discordUserId: string) {
  return db.person.findUnique({ where: { discordUserId }, select: { id: true, status: true } });
}

export async function findLoginableByEmail(db: Db, email: string) {
  return db.person.findFirst({
    where: { clubEmail: email.trim().toLowerCase(), status: 'Active' },
    select: { id: true },
  });
}

async function assertDepartmentActive(tx: Tx, departmentId: string): Promise<void> {
  const department = await tx.department.findUnique({ where: { id: departmentId }, select: { active: true } });
  if (!department?.active) {
    throw new AppError('VALIDATION', 'Department does not exist or is inactive.');
  }
}

async function createPersonTx(tx: Tx, input: PersonInput, actorId: string): Promise<string> {
  const data = normalizePatch(input) as Prisma.PersonUncheckedCreateInput;
  data.kind = input.kind;

  await assertDepartmentActive(tx, input.departmentId);
  const person = await tx.person.create({ data, select: { id: true } });

  const settings = await getSettings(tx);
  const defaultRoleId = input.kind === 'Candidate' ? settings.probationRoleId : settings.memberRoleId;
  if (defaultRoleId) {
    await tx.personRole.create({ data: { personId: person.id, discordRoleId: defaultRoleId } });
  }

  await writeAudit(tx, {
    actorId,
    action: 'PersonCreated',
    entityType: 'Person',
    entityId: person.id,
    metadata: { kind: input.kind, studentId: data.studentId, defaultRoleId },
  });
  return person.id;
}

export async function createPerson(db: Db, input: PersonInput, actorId: string) {
  try {
    return await db.$transaction(async (tx) => getPerson(tx, await createPersonTx(tx, input, actorId)));
  } catch (error) {
    rethrowUniqueViolation(error, uniqueMessage);
  }
}

// All or nothing: one bad row rolls back the whole file.
export async function importPeople(db: Db, inputs: readonly PersonInput[], actorId: string): Promise<number> {
  try {
    return await db.$transaction(async (tx) => {
      for (const input of inputs) await createPersonTx(tx, input, actorId);
      return inputs.length;
    }, { timeout: 60_000 });
  } catch (error) {
    rethrowUniqueViolation(error, uniqueMessage);
  }
}

export async function findExistingStudentIds(db: Db, studentIds: readonly string[]): Promise<Set<string>> {
  const rows = await db.person.findMany({ where: { studentId: { in: [...studentIds] } }, select: { studentId: true } });
  return new Set(rows.map((row) => row.studentId));
}

// DB only: Discord roles and server membership are left as they are. Roles/mentoring cascade, audit rows keep a null actor.
export async function deletePerson(db: Db, id: string, actorId: string) {
  await db.$transaction(async (tx) => {
    const before = await getPerson(tx, id);
    if (before.isAdmin) {
      const otherActiveAdmins = await tx.person.count({
        where: { isAdmin: true, status: 'Active', clubEmail: { not: null }, id: { not: id } },
      });
      assertKeepsAnAdmin(otherActiveAdmins, before, true);
    }
    await tx.person.delete({ where: { id } });
    await writeAudit(tx, {
      actorId: actorId === id ? null : actorId,
      action: 'PersonDeleted',
      entityType: 'Person',
      entityId: id,
      metadata: { kind: before.kind, studentId: before.studentId, fullName: before.fullName, discordUserId: before.discordUserId },
    });
  });
}

export async function updatePerson(db: Db, id: string, patch: PersonPatch, actorId: string) {
  const data = normalizePatch(patch);

  try {
    return await db.$transaction(async (tx) => {
      const before = await getPerson(tx, id);
      const losesAdmin = patch.isAdmin === false || patch.status === 'Inactive' || patch.clubEmail === null;
      if (before.isAdmin && losesAdmin) {
        const otherActiveAdmins = await tx.person.count({
          where: { isAdmin: true, status: 'Active', clubEmail: { not: null }, id: { not: id } },
        });
        assertKeepsAnAdmin(otherActiveAdmins, before, true);
      }
      if (patch.departmentId !== undefined && patch.departmentId !== before.department.id) {
        await assertDepartmentActive(tx, patch.departmentId);
      }

      await tx.person.update({ where: { id }, data });
      await writeAudit(tx, {
        actorId,
        action: 'PersonUpdated',
        entityType: 'Person',
        entityId: id,
        metadata: JSON.parse(JSON.stringify(data)) as Prisma.InputJsonValue,
      });
      return getPerson(tx, id);
    });
  } catch (error) {
    rethrowUniqueViolation(error, uniqueMessage);
  }
}
