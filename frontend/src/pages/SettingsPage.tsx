import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type Settings, type SyncAllReport } from '../api';
import { useRoleMap, useRoles } from '../Layout';
import { Button, Card, Field, Loading, PageHeader, Select, useToast } from '../ui';

export function SettingsPage() {
  const queryClient = useQueryClient();
  const settings = useQuery({ queryKey: ['settings'], queryFn: () => api<Settings>('/settings') });
  const roles = useRoles();
  const roleMap = useRoleMap();
  const toast = useToast();
  const [report, setReport] = useState<{ dryRun: boolean; data: SyncAllReport } | null>(null);
  const [busy, setBusy] = useState(false);

  if (settings.isLoading) return <Loading />;
  const assignable = (roles.data ?? []).filter((role) => role.assignable);

  const save = async (patch: Partial<Settings>) => {
    try {
      await api('/settings', { method: 'PATCH', body: patch });
      await queryClient.invalidateQueries({ queryKey: ['settings'] });
      toast.success('Đã lưu cài đặt.');
    } catch (e) {
      toast.error(e);
    }
  };

  const syncAll = async (dryRun: boolean) => {
    if (!dryRun && !confirm('Áp role trong hệ thống lên Discord cho tất cả mọi người? Role không có trong hệ thống sẽ bị gỡ.')) return;
    setBusy(true);
    try {
      setReport({ dryRun, data: await api<SyncAllReport>('/sync-all', { method: 'POST', body: { dryRun } }) });
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  const roleSelect = (key: keyof Settings, label: string, hint: string) => (
    <Field label={label} hint={hint}>
      <Select value={settings.data?.[key] ?? ''} onChange={(e) => void save({ [key]: e.target.value || null })}>
        <option value="">— chưa chọn —</option>
        {assignable.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
      </Select>
    </Field>
  );

  return (
    <>
      <PageHeader title="Cài đặt" />
      <Card title="Role mặc định">
        <div className="grid gap-3 sm:grid-cols-2">
          {roleSelect('probationRoleId', 'Role Probation', 'Gán khi tạo probation mới; gỡ khi kết thúc đợt cho người đã duyệt.')}
          {roleSelect('memberRoleId', 'Role Member', 'Gán khi tạo thành viên mới, hoặc cho probation đã duyệt khi kết thúc đợt.')}
        </div>
      </Card>
      <Card
        title="Đồng bộ toàn bộ"
        actions={
          <>
            <Button variant="secondary" disabled={busy} onClick={() => void syncAll(true)}>Xem trước</Button>
            <Button variant="danger" disabled={busy} onClick={() => void syncAll(false)}>Áp dụng</Button>
          </>
        }
      >
        <p className="text-sm text-slate-500">Đưa role trên Discord của mọi người đang hoạt động về đúng như trong hệ thống.</p>
        {busy && <p className="mt-2 text-sm text-slate-500">Đang chạy…</p>}
        {report && (
          <div className="mt-3 space-y-2 text-sm">
            <p>
              {report.dryRun ? 'Xem trước' : 'Đã áp dụng'}: kiểm tra {report.data.checked} người, {report.data.changed.length} người {report.dryRun ? 'sẽ' : 'đã'} thay đổi,{' '}
              {report.data.notInServer} không có trong server, {report.data.failed.length} lỗi.
            </p>
            <ul className="max-h-80 space-y-1 overflow-y-auto">
              {report.data.changed.map((change) => (
                <li key={change.personId}>
                  <span className="font-medium">{change.fullName}</span>
                  {change.add.length > 0 && <span className="text-emerald-700 dark:text-emerald-400"> +{change.add.map((r) => roleMap.get(r)?.name ?? r).join(', ')}</span>}
                  {change.remove.length > 0 && <span className="text-rose-700 dark:text-rose-400"> −{change.remove.map((r) => roleMap.get(r)?.name ?? r).join(', ')}</span>}
                </li>
              ))}
              {report.data.failed.map((failure) => <li key={failure.personId} className="text-rose-600">{failure.fullName}: {failure.error}</li>)}
            </ul>
          </div>
        )}
      </Card>
    </>
  );
}
