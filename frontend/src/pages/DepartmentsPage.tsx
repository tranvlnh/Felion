import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type Department } from '../api';
import { Badge, Button, Card, EmptyState, Input, Loading, PageHeader, useToast } from '../ui';

export function DepartmentsPage() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const departments = useQuery({ queryKey: ['departments'], queryFn: () => api<Department[]>('/departments') });
  const [name, setName] = useState('');
  const [editing, setEditing] = useState<{ id: string; name: string } | null>(null);

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      await queryClient.invalidateQueries({ queryKey: ['departments'] });
      toast.success(success);
      return true;
    } catch (e) {
      toast.error(e);
      return false;
    }
  };

  const rename = async () => {
    if (!editing?.name.trim()) return;
    if (await run(() => api(`/departments/${editing.id}`, { method: 'PATCH', body: { name: editing.name.trim() } }), 'Đã đổi tên ban.')) setEditing(null);
  };

  return (
    <>
      <PageHeader title="Ban" subtitle="Ban ngừng dùng vẫn giữ cho người đang ở ban đó, nhưng không chọn được cho người mới." />
      <Card title="Thêm ban">
        <form
          className="flex flex-wrap gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            void run(() => api('/departments', { method: 'POST', body: { name: name.trim() } }), 'Đã thêm ban.').then((ok) => { if (ok) setName(''); });
          }}
        >
          <Input required className="max-w-xs" placeholder="Tên ban" value={name} onChange={(e) => setName(e.target.value)} aria-label="Tên ban" />
          <Button type="submit" disabled={!name.trim()}>Thêm</Button>
        </form>
      </Card>
      <Card>
        {departments.isLoading ? <Loading /> : departments.data?.length === 0 ? <EmptyState>Chưa có ban nào.</EmptyState> : (
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {departments.data?.map((d) => (
              <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                {editing?.id === d.id ? (
                  <form className="flex flex-1 flex-wrap gap-2" onSubmit={(e) => { e.preventDefault(); void rename(); }}>
                    <Input autoFocus className="max-w-xs" value={editing.name} onChange={(e) => setEditing({ id: d.id, name: e.target.value })} onKeyDown={(e) => { if (e.key === 'Escape') setEditing(null); }} aria-label="Tên mới" />
                    <Button type="submit">Lưu</Button>
                    <Button variant="secondary" onClick={() => setEditing(null)}>Hủy</Button>
                  </form>
                ) : (
                  <>
                    <span className={d.active ? '' : 'text-slate-500'}>{d.name} {!d.active && <Badge tone="red">ngừng</Badge>}</span>
                    <span className="flex gap-2">
                      <Button variant="secondary" onClick={() => setEditing({ id: d.id, name: d.name })}>Đổi tên</Button>
                      <Button variant="secondary" onClick={() => void run(() => api(`/departments/${d.id}`, { method: 'PATCH', body: { active: !d.active } }), d.active ? 'Đã ngừng dùng ban.' : 'Đã dùng lại ban.')}>
                        {d.active ? 'Ngừng dùng' : 'Dùng lại'}
                      </Button>
                    </span>
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}
