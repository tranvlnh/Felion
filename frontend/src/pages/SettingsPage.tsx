import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type Channel, type Settings, type SyncAllReport } from '../api';
import { useRoleMap, useRoles } from '../Layout';
import { Button, Card, Field, Input, Loading, PageHeader, Select, Textarea, useToast } from '../ui';

function VerificationCard({ settings }: { settings: Settings }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const channels = useQuery({ queryKey: ['channels'], queryFn: () => api<Channel[]>('/channels') });
  const [form, setForm] = useState({
    channelId: settings.verificationChannelId ?? '',
    verificationTitle: settings.verificationTitle,
    verificationDescription: settings.verificationDescription,
    verificationButtonLabel: settings.verificationButtonLabel,
  });
  const [busy, setBusy] = useState(false);
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value });
  const published = settings.verificationMessageId !== null && settings.verificationChannelId === form.channelId;

  const publish = async () => {
    setBusy(true);
    try {
      await api('/verification-message', { method: 'POST', body: form });
      await queryClient.invalidateQueries({ queryKey: ['settings'] });
      toast.success(published ? 'Đã cập nhật tin nhắn trên Discord.' : 'Đã đăng tin nhắn lên Discord.');
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card
      title="Tin nhắn xác minh"
      actions={
        <Button disabled={busy || !form.channelId} onClick={() => void publish()}>
          {published ? 'Lưu & cập nhật' : 'Lưu & đăng'}
        </Button>
      }
    >
      <p className="mb-3 text-sm text-slate-500">
        Tin nhắn trên Discord có nút để mọi người nhập Mã sinh viên, liên kết tài khoản và nhận role.
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Kênh" hint="Chỉ hiện kênh bot gửi được tin nhắn. Đổi kênh sẽ xoá tin nhắn cũ.">
          <Select value={form.channelId} onChange={set('channelId')}>
            <option value="">— chưa chọn —</option>
            {(channels.data ?? []).map((channel) => <option key={channel.id} value={channel.id}>#{channel.name}</option>)}
          </Select>
        </Field>
        <Field label="Nhãn nút">
          <Input value={form.verificationButtonLabel} maxLength={80} onChange={set('verificationButtonLabel')} />
        </Field>
        <div className="sm:col-span-2">
          <Field label="Tiêu đề">
            <Input value={form.verificationTitle} maxLength={256} onChange={set('verificationTitle')} />
          </Field>
        </div>
        <div className="sm:col-span-2">
          <Field label="Nội dung">
            <Textarea rows={4} value={form.verificationDescription} maxLength={4096} onChange={set('verificationDescription')} />
          </Field>
        </div>
      </div>
    </Card>
  );
}

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
      {settings.data && <VerificationCard settings={settings.data} />}
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
