import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, discordPermissionFlags, webPermissions, type GuildRole, type WebPermission } from '../api';
import { useMe, useRoles } from '../Layout';
import { Badge, Button, Card, ErrorText, Field, Input, Loading, PageHeader, RoleChip } from '../ui';

export function RolesPage() {
  const me = useMe();
  const roles = useRoles();
  const [editing, setEditing] = useState<GuildRole | 'new' | null>(null);

  return (
    <>
      <PageHeader title="Role Discord" subtitle="Role thật của server. Gắn quyền web cho role để người có role đó dùng được trang quản trị." actions={me.isAdmin && <Button onClick={() => setEditing('new')}>Tạo role</Button>} />
      {editing && <RoleEditor key={editing === 'new' ? 'new' : editing.id} role={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <Card>
        <ErrorText error={roles.error} />
        {roles.isLoading ? <Loading /> : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800">
                <tr>
                  <th className="py-2 pr-3">Role</th>
                  <th className="py-2 pr-3">Quyền web</th>
                  <th className="py-2 pr-3">Ghi chú</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {roles.data?.map((role) => (
                  <tr key={role.id}>
                    <td className="py-2 pr-3"><RoleChip roleId={role.id} role={role} /></td>
                    <td className="py-2 pr-3">
                      <div className="flex flex-wrap gap-1">
                        {role.webPermissions.length === 0 ? <span className="text-slate-400">—</span> : role.webPermissions.map((p) => <Badge key={p} tone="indigo">{p}</Badge>)}
                      </div>
                    </td>
                    <td className="py-2 pr-3 text-xs text-slate-500">
                      {role.managed ? 'do integration quản lý' : !role.assignable ? 'nằm trên role của bot' : ''}
                      {(BigInt(role.permissions) & (1n << 3n)) !== 0n && <Badge tone="red">Administrator</Badge>}
                    </td>
                    <td className="py-2 text-right">
                      {me.isAdmin && role.assignable && <Button variant="secondary" onClick={() => setEditing(role)}>Sửa</Button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </>
  );
}

function RoleEditor({ role, onClose }: { role: GuildRole | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(role?.name ?? '');
  const [color, setColor] = useState(role ? `#${role.color.toString(16).padStart(6, '0')}` : '#99aab5');
  const [permissions, setPermissions] = useState(BigInt(role?.permissions ?? '0'));
  const [web, setWeb] = useState<WebPermission[]>(role?.webPermissions ?? []);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const save = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      await queryClient.invalidateQueries({ queryKey: ['roles'] });
      onClose();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  const body = { name: name.trim(), color: Number.parseInt(color.slice(1), 16), permissions: permissions.toString(), webPermissions: web };

  return (
    <Card title={role ? `Sửa role ${role.name}` : 'Tạo role mới'} actions={<Button variant="secondary" onClick={onClose}>Đóng</Button>}>
      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          void save(() => role ? api(`/roles/${role.id}`, { method: 'PATCH', body }) : api('/roles', { method: 'POST', body }));
        }}
      >
        <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
          <Field label="Tên"><Input required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} /></Field>
          <Field label="Màu"><Input type="color" className="h-9 w-16 p-1" value={color} onChange={(e) => setColor(e.target.value)} /></Field>
        </div>
        <fieldset>
          <legend className="mb-2 text-xs font-medium text-slate-600 dark:text-slate-400">Quyền trên web</legend>
          <div className="grid gap-1 sm:grid-cols-2">
            {webPermissions.map((p) => (
              <label key={p.value} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={web.includes(p.value)} onChange={(e) => setWeb((list) => e.target.checked ? [...list, p.value] : list.filter((x) => x !== p.value))} />
                {p.label}
              </label>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend className="mb-2 text-xs font-medium text-slate-600 dark:text-slate-400">Quyền trên Discord</legend>
          <div className="grid gap-1 sm:grid-cols-3">
            {discordPermissionFlags.map((flag) => (
              <label key={flag.label} className={`flex items-center gap-2 text-sm ${flag.danger ? 'text-rose-700 dark:text-rose-400' : ''}`}>
                <input
                  type="checkbox"
                  checked={(permissions & flag.bit) !== 0n}
                  onChange={(e) => setPermissions((value) => e.target.checked ? value | flag.bit : value & ~flag.bit)}
                />
                {flag.label}
              </label>
            ))}
          </div>
          <p className="mt-2 text-xs text-slate-500">Quyền màu đỏ là quyền quản trị: role có quyền này (hoặc có quyền web) chỉ admin được gán cho người khác.</p>
        </fieldset>
        <ErrorText error={error} />
        <div className="flex flex-wrap justify-between gap-2">
          <Button type="submit" disabled={busy}>{busy ? 'Đang lưu…' : role ? 'Lưu' : 'Tạo'}</Button>
          {role && (
            <Button
              type="button"
              variant="danger"
              disabled={busy}
              onClick={() => { if (confirm(`Xóa role ${role.name} khỏi server Discord? Không hoàn tác được.`)) void save(() => api(`/roles/${role.id}`, { method: 'DELETE' })); }}
            >
              Xóa role
            </Button>
          )}
        </div>
      </form>
    </Card>
  );
}
