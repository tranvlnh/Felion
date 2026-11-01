import { AppError } from '#app/errors.js';
import { writeAudit } from './audit.js';
import { rethrowUniqueViolation, type Db } from './client.js';

export type DepartmentInput = { name: string; active?: boolean | undefined };
type DepartmentPatch = { [K in keyof DepartmentInput]?: DepartmentInput[K] | undefined };

type DepartmentData = { name?: string; active?: boolean };

function normalize(input: DepartmentPatch): DepartmentData {
  const out: DepartmentData = {};
  if (input.name !== undefined) {
    out.name = input.name.trim();
    if (out.name.length < 1 || out.name.length > 100) {
      throw new AppError('VALIDATION', 'Department name must contain between 1 and 100 characters.');
    }
  }
  if (input.active !== undefined) {
    out.active = input.active;
  }
  return out;
}

export function listDepartments(db: Db) {
  return db.department.findMany({ orderBy: { name: 'asc' } });
}

export async function createDepartment(db: Db, input: DepartmentInput, actorId: string) {
  const data = normalize(input) as DepartmentData & { name: string };
  try {
    return await db.$transaction(async (tx) => {
      const department = await tx.department.create({ data });
      await writeAudit(tx, { actorId, action: 'DepartmentCreated', entityType: 'Department', entityId: department.id, metadata: data });
      return department;
    });
  } catch (error) {
    rethrowUniqueViolation(error, 'A department with this name already exists.');
  }
}

export async function updateDepartment(db: Db, id: string, input: DepartmentPatch, actorId: string) {
  const data = normalize(input);
  try {
    return await db.$transaction(async (tx) => {
      const department = await tx.department.update({ where: { id }, data });
      await writeAudit(tx, { actorId, action: 'DepartmentUpdated', entityType: 'Department', entityId: id, metadata: data });
      return department;
    });
  } catch (error) {
    rethrowUniqueViolation(error, 'A department with this name already exists.');
  }
}
