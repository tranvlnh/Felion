import type { Settings } from '#app/generated/prisma/client.js';
import { writeAudit } from './audit.js';
import { definedOnly, type Db, type Tx } from './client.js';

export type SettingsValues = {
  probationRoleId: string | null;
  memberRoleId: string | null;
  verificationChannelId: string | null;
  verificationMessageId: string | null;
  verificationTitle: string;
  verificationDescription: string;
  verificationButtonLabel: string;
};

type StoredSettings = { [K in keyof SettingsValues]?: SettingsValues[K] | undefined };

function toValues(settings: Settings | null): SettingsValues {
  return {
    probationRoleId: settings?.probationRoleId ?? null,
    memberRoleId: settings?.memberRoleId ?? null,
    verificationChannelId: settings?.verificationChannelId ?? null,
    verificationMessageId: settings?.verificationMessageId ?? null,
    verificationTitle: settings?.verificationTitle ?? 'Felion verification',
    verificationDescription: settings?.verificationDescription
      ?? 'Nhấn nút bên dưới và nhập Mã sinh viên của bạn để liên kết tài khoản Discord.',
    verificationButtonLabel: settings?.verificationButtonLabel ?? 'Nhấn để mở',
  };
}

export async function getSettings(db: Db | Tx): Promise<SettingsValues> {
  return toValues(await db.settings.findUnique({ where: { id: 1 } }));
}

export async function updateSettings(db: Db, patch: StoredSettings, actorId: string): Promise<SettingsValues> {
  const values = definedOnly(patch);
  return db.$transaction(async (tx) => {
    const settings = await tx.settings.upsert({ where: { id: 1 }, create: { id: 1, ...values }, update: values });
    await writeAudit(tx, { actorId, action: 'SettingsUpdated', entityType: 'Settings', metadata: values });
    return toValues(settings);
  });
}
