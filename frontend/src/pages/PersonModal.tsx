import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { api, can, isPrivilegedRole, type PersonDetail, type SyncOutcome, type WriteResult } from '../api';
import { useMe, useRoleMap, useRoles } from '../Layout';
import { Avatar, Badge, Button, Card, ErrorText, formatDate, Loading, Modal, Notice, RoleChip, Select, useToast } from '../ui';
import { PersonForm, toBody, toFormValues } from './PersonForm';

const syncLabels: Record<SyncOutcome['status'], string> = {
  Synced: 'Đã đồng bộ',
  Preview: '',
  NotLinked: 'Chưa liên kết Discord: role sẽ được áp khi người này dùng /verification.',
  Inactive: 'Không hoạt động: không đồng bộ role.',
  NotInServer: 'Không có trong server Discord: role sẽ được trả lại khi vào lại server.',
};

export function PersonModal({ id, onClose }: { id: string; onClose: () => void }) {
  return (
    <Modal title="Chi tiết người" onClose={onClose}>
      <PersonDetailView id={id} onDeleted={onClose} />
    </Modal>
  );
}

function PersonDetailView({ id, onDeleted }: { id: string; onDeleted: () => void }) {
  const me = useMe();
  const queryClient = useQueryClient();
  const roleMap = useRoleMap();
  const roles = useRoles();
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  const [newRole, setNewRole] = useState('');

  const person = useQuery({ queryKey: ['person', id], queryFn: () => api<PersonDetail>(`/people/${id}`) });
  const preview = useQuery({
    queryKey: ['sync-preview', id],
    queryFn: () => api<SyncOutcome>(`/people/${id}/sync-preview`),
    enabled: person.isSuccess,
  });

  if (person.isLoading) return <Loading />;
  if (!person.data) return <ErrorText error={person.error} />;
  const p = person.data;

  const targetOk = me.isAdmin || !p.isAdmin;
  const canManage = can(me, 'ManagePeople') && targetOk;
  const canAssign = can(me, 'AssignRoles') && targetOk;
  const canKick = can(me, 'KickMembers') && targetOk && p.status === 'Active';
  const canTouchRole = (roleId: string) => {
    const role = roleMap.get(roleId);
    return canAssign && (me.isAdmin || !role || !isPrivilegedRole(role));
  };

  const run = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    try {
      const result = await action() as Partial<WriteResult> | SyncOutcome | undefined;
      if (result && 'syncError' in result && result.syncError) {
        toast.warn(`Đã lưu, nhưng chưa đồng bộ được với Discord: ${result.syncError}`);
      } else if (success) {
        toast.success(success);
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['person', id] }),
        queryClient.invalidateQueries({ queryKey: ['sync-preview', id] }),
        queryClient.invalidateQueries({ queryKey: ['people'] }),
        queryClient.invalidateQueries({ queryKey: ['probation'] }),
      ]);
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  const post = (path: string, body: unknown = {}) => api(`/people/${id}${path}`, { method: 'POST', body });
  const plan = preview.data?.plan;
  const pending = plan ? plan.add.length + plan.remove.length : 0;
  const addable = (roles.data ?? []).filter((role) => role.assignable && !p.roles.includes(role.id) && (me.isAdmin || !isPrivilegedRole(role)));
  const initial = toFormValues(p);

  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-4 rounded-xl bg-white p-5 shadow-xs ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800">
        <div className="flex min-w-0 items-center gap-4">
          <Avatar discordUserId={p.discordUserId} name={p.fullName} className="size-16 text-xl" />
          <div className="min-w-0 space-y-1.5">
            <h2 className="text-2xl font-semibold tracking-tight">{p.fullName}</h2>
            <div className="flex flex-wrap gap-1.5">
              {p.kind === 'Candidate'
                ? <Badge tone={p.probationApproved ? 'green' : 'amber'}>{p.probationApproved ? 'Probation · đã duyệt' : 'Probation · chưa duyệt'}</Badge>
                : <Badge>Thành viên</Badge>}
              {p.isAdmin && <Badge tone="indigo">admin</Badge>}
              {p.status === 'Active' ? <Badge tone="green">Đang hoạt động</Badge> : <Badge>Không hoạt động</Badge>}
            </div>
            <p className="text-sm text-slate-500">
              Discord: {p.discordUserId ? <span className="font-mono">{p.discordUserId}</span> : 'chưa liên kết'} · Email CLB: {p.clubEmail ?? '—'}
            </p>
            <p className="text-sm text-slate-500">
              SĐT: {p.phone ?? '—'} · Email cá nhân: {p.personalEmail ?? '—'} · Ngày sinh: {p.birthDate ? formatDate(p.birthDate) : '—'}
              {' · '}Facebook: {p.facebookUrl ? <a href={p.facebookUrl} target="_blank" rel="noreferrer" className="text-indigo-700 hover:underline dark:text-indigo-300">mở</a> : '—'}
            </p>
            {(p.kind === 'Candidate' || p.mentorTeams.length > 0) && (
              <p className="text-sm text-slate-500">
                {p.kind === 'Candidate' ? <>Team: {p.team?.name ?? 'chưa có'}</> : <>Mentor team: {p.mentorTeams.map((t) => t.name).join(', ')}</>}
                {' · '}<Link to="/probation" className="text-indigo-700 hover:underline dark:text-indigo-300">Quản lý ở trang Probation</Link>
              </p>
            )}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
            {canManage && p.kind === 'Candidate' && p.status === 'Active' && (
              <Button
                variant={p.probationApproved ? 'secondary' : 'primary'}
                disabled={busy}
                title="Người đã duyệt lên thành viên khi kết thúc đợt probation"
                onClick={() => void run(
                  () => api(`/probation/candidates/${id}/approval`, { method: 'PUT', body: { approved: !p.probationApproved } }),
                  p.probationApproved ? 'Đã bỏ duyệt.' : 'Đã duyệt; sẽ lên thành viên khi kết thúc đợt.',
                )}
              >{p.probationApproved ? 'Bỏ duyệt' : 'Duyệt lên thành viên'}</Button>
            )}
            {canAssign && <Button variant="secondary" disabled={busy} onClick={() => void run(() => post('/sync'), 'Đã đồng bộ role với Discord.')}>Sync role</Button>}
            {canManage && (
              <Button variant="secondary" disabled={busy} onClick={() => {
                const next = p.status === 'Active' ? 'Inactive' : 'Active';
                if (next === 'Active' || confirm(`Chuyển ${p.fullName} sang không hoạt động? Người này sẽ không đăng nhập web được và không được đồng bộ role.`)) {
                  void run(() => api(`/people/${id}`, { method: 'PATCH', body: { status: next } }), next === 'Active' ? 'Đã chuyển sang đang hoạt động.' : 'Đã chuyển sang không hoạt động.');
                }
              }}>{p.status === 'Active' ? 'Đặt không hoạt động' : 'Đặt đang hoạt động'}</Button>
            )}
            {canKick && (
              <Button variant="danger" disabled={busy} onClick={() => {
                const reason = prompt(`Lý do kick ${p.fullName}? Người này cũng sẽ chuyển sang không hoạt động.`);
                if (reason?.trim()) void run(() => post('/kick', { reason: reason.trim() }), 'Đã kick và chuyển sang không hoạt động.');
              }}>Kick</Button>
            )}
            {canManage && (
              <Button variant="danger" disabled={busy} onClick={async () => {
                if (!confirm(`Xóa ${p.fullName} khỏi hệ thống? Không thể hoàn tác. Role và tài khoản trên Discord giữ nguyên.`)) return;
                setBusy(true);
                try {
                  await api(`/people/${id}`, { method: 'DELETE' });
                  toast.success('Đã xóa.');
                  queryClient.removeQueries({ queryKey: ['person', id] });
                  await Promise.all([
                    queryClient.invalidateQueries({ queryKey: ['people'] }),
                    queryClient.invalidateQueries({ queryKey: ['probation'] }),
                  ]);
                  onDeleted();
                } catch (e) {
                  toast.error(e);
                  setBusy(false);
                }
              }}>Xóa</Button>
            )}
        </div>
      </div>

      <div className="grid items-start gap-4 lg:grid-cols-5">
      <Card title="Thông tin" className="lg:col-span-3">
        <PersonForm
          key={p.updatedAt}
          initial={initial}
          showKind={false}
          submitLabel="Lưu"
          disabled={!canManage}
          onSubmit={(values) => run(() => api(`/people/${id}`, { method: 'PATCH', body: toBody(values, initial, false) }), 'Đã lưu.')}
        />
      </Card>

      <Card title="Role" className="lg:col-span-2">
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            {p.roles.length === 0 && <span className="text-sm text-slate-500">Chưa có role nào.</span>}
            {p.roles.map((roleId) => (
              <RoleChip
                key={roleId}
                roleId={roleId}
                role={roleMap.get(roleId)}
                onRemove={canTouchRole(roleId) && !busy ? () => void run(() => api(`/people/${id}/roles/${roleId}`, { method: 'DELETE' })) : undefined}
              />
            ))}
          </div>
          {canAssign && (
            <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (newRole) void run(() => post('/roles', { roleId: newRole })).then(() => setNewRole('')); }}>
              <Select value={newRole} onChange={(e) => setNewRole(e.target.value)} aria-label="Role để thêm">
                <option value="">Chọn role để thêm…</option>
                {addable.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
              </Select>
              <Button type="submit" disabled={!newRole || busy}>Thêm</Button>
            </form>
          )}
          {preview.isLoading && <p className="text-xs text-slate-500">Đang kiểm tra trạng thái Discord…</p>}
          {preview.error != null && <p className="text-xs text-rose-600">Không kiểm tra được Discord: {(preview.error as Error).message}</p>}
          {preview.data && preview.data.status !== 'Preview' && <p className="text-xs text-slate-500">{syncLabels[preview.data.status]}</p>}
          {plan && pending === 0 && <p className="text-xs text-emerald-700 dark:text-emerald-400">Discord đã khớp với hệ thống.</p>}
          {plan && pending > 0 && (
            <Notice>
              Chưa đồng bộ: Discord sẽ {plan.add.length > 0 && <>thêm {plan.add.map((r) => roleMap.get(r)?.name ?? r).join(', ')}</>}
              {plan.add.length > 0 && plan.remove.length > 0 && ' và '}
              {plan.remove.length > 0 && <>gỡ {plan.remove.map((r) => roleMap.get(r)?.name ?? r).join(', ')}</>} khi bấm Sync.
            </Notice>
          )}
          {plan && plan.unmanageable.length > 0 && (
            <p className="text-xs text-amber-700 dark:text-amber-400">
              Bot không gán được: {plan.unmanageable.map((r) => roleMap.get(r)?.name ?? r).join(', ')} (role do integration quản lý, hoặc nằm trên role của bot).
            </p>
          )}
        </div>
      </Card>
      </div>
    </>
  );
}
