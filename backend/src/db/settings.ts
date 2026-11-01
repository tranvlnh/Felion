import { writeAudit } from './audit.js';
import { definedOnly, type Db, type Tx } from './client.js';

export type SettingsValues = {
  probationRoleId: string | null;
  memberRoleId: string | null;
};

export async function getSettings(db: Db | Tx): Promise<SettingsValues> {
  const settings = await db.settings.findUnique({ where: { id: 1 } });
  return {
    probationRoleId: settings?.probationRoleId ?? null,
    memberRoleId: settings?.memberRoleId ?? null,
  };
}

export async function updateSettings(
  db: Db,
  patch: { [K in keyof SettingsValues]?: SettingsValues[K] | undefined },
  actorId: string,
): Promise<SettingsValues> {
  const values = definedOnly(patch);
  return db.$transaction(async (tx) => {
    const settings = await tx.settings.upsert({ where: { id: 1 }, create: { id: 1, ...values }, update: values });
    await writeAudit(tx, { actorId, action: 'SettingsUpdated', entityType: 'Settings', metadata: values });
    return { probationRoleId: settings.probationRoleId, memberRoleId: settings.memberRoleId };
  });
}
