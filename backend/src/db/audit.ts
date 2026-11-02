import type { Prisma } from '#app/generated/prisma/client.js';
import type { Db, Tx } from './client.js';

export type AuditEntry = {
  actorId: string | null;
  action: string;
  entityType: string;
  entityId?: string | null;
  metadata?: Prisma.InputJsonValue;
};

// An array is written with one query (bulk actions such as CSV import).
export async function writeAudit(db: Db | Tx, entries: AuditEntry | readonly AuditEntry[]): Promise<void> {
  await db.auditLog.createMany({
    data: (Array.isArray(entries) ? entries : [entries]).map((entry: AuditEntry) => ({
      actorId: entry.actorId,
      action: entry.action,
      entityType: entry.entityType,
      entityId: entry.entityId ?? null,
      ...(entry.metadata === undefined ? {} : { metadata: entry.metadata }),
    })),
  });
}

export async function listAudit(db: Db, page: number, pageSize: number) {
  const [items, total] = await Promise.all([
    db.auditLog.findMany({
      orderBy: { createdAt: 'desc' },
      skip: (page - 1) * pageSize,
      take: pageSize,
      include: { actor: { select: { id: true, fullName: true } } },
    }),
    db.auditLog.count(),
  ]);
  return { items, total };
}
