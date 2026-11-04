import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, can, type Department, type Page, type Person } from '../api';
import { useMe, usePersonHref } from '../Layout';
import { Avatar, Badge, Button, buttonClass, Card, EmptyState, ErrorText, Input, Loading, PageHeader, Pager, Select } from '../ui';
import { ImportCsv } from './ImportCsv';

const pageSize = 50;
const filterKeys = ['search', 'status', 'departmentId', 'generation'] as const;

export function PeoplePage() {
  const me = useMe();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const personHref = usePersonHref();
  const page = Number(params.get('page') ?? '1');
  const [search, setSearch] = useState(params.get('search') ?? '');
  const [generation, setGeneration] = useState(params.get('generation') ?? '');

  const setFilter = (key: string, value: string) => setParams((prev) => {
    const next = new URLSearchParams(prev);
    if (value) next.set(key, value); else next.delete(key);
    next.delete('page');
    return next;
  });

  // Typing filters on its own after a short pause; no Enter needed.
  useEffect(() => {
    const timer = setTimeout(() => {
      if (search.trim() !== (params.get('search') ?? '')) setFilter('search', search.trim());
      if (generation !== (params.get('generation') ?? '')) setFilter('generation', generation);
    }, 300);
    return () => clearTimeout(timer);
  }, [search, generation]);

  // Candidates live on the Probation page.
  const query = new URLSearchParams({ kind: 'Member', page: String(page), pageSize: String(pageSize) });
  for (const key of filterKeys) {
    const value = params.get(key);
    if (value) query.set(key, value);
  }
  const filtered = filterKeys.some((key) => params.get(key));
  const exportQuery = new URLSearchParams(query);
  exportQuery.delete('page');
  exportQuery.delete('pageSize');

  const people = useQuery({
    queryKey: ['people', query.toString()],
    queryFn: () => api<Page<Person>>(`/people?${query}`),
    placeholderData: keepPreviousData,
  });
  const departments = useQuery({ queryKey: ['departments'], queryFn: () => api<Department[]>('/departments') });

  const clearFilters = () => {
    setSearch('');
    setGeneration('');
    setParams(new URLSearchParams());
  };

  return (
    <>
      <PageHeader
        title="Thành viên"
        subtitle={people.data ? `${people.data.total} người${filtered ? ' khớp bộ lọc' : ''}` : undefined}
        actions={
          <>
            <a href={`/api/people/export.csv?${exportQuery}`} className={buttonClass('secondary')}>Xuất CSV</a>
            {can(me, 'ManagePeople') && <Link to={`?${new URLSearchParams([...params, ['create', 'Member']])}`} className={buttonClass()}>+ Thêm người</Link>}
          </>
        }
      />
      {can(me, 'ManagePeople') && <ImportCsv kind="Member" />}
      <Card>
        <div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-[2fr_1fr_1fr_6rem_auto]">
          <Input className="sm:col-span-3 lg:col-span-1" type="search" placeholder="Tìm tên, MSSV, email, Discord id…" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Tìm kiếm" />
          <Select value={params.get('status') ?? ''} onChange={(e) => setFilter('status', e.target.value)} aria-label="Trạng thái">
            <option value="">Mọi trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Inactive">Không hoạt động</option>
          </Select>
          <Select value={params.get('departmentId') ?? ''} onChange={(e) => setFilter('departmentId', e.target.value)} aria-label="Ban">
            <option value="">Mọi ban</option>
            {departments.data?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
          <Input type="number" min={1} placeholder="Gen" value={generation} onChange={(e) => setGeneration(e.target.value)} aria-label="Gen" />
          <Button variant="secondary" disabled={!filtered && !search && !generation} onClick={clearFilters}>Xóa lọc</Button>
        </div>
      </Card>
      <Card>
        <ErrorText error={people.error} />
        {people.isLoading ? <Loading /> : people.data?.items.length === 0 ? (
          <EmptyState>{filtered ? 'Không có ai khớp bộ lọc.' : 'Chưa có ai.'}</EmptyState>
        ) : (
          <div className="-mx-5 -mt-5 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 bg-slate-50/80 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800 dark:bg-slate-800/40">
                <tr>
                  <th className="px-5 py-2.5 font-medium">Tên</th>
                  <th className="px-3 py-2.5 font-medium">MSSV</th>
                  <th className="px-3 py-2.5 font-medium">Ban</th>
                  <th className="px-3 py-2.5 font-medium">Gen</th>
                  <th className="px-5 py-2.5 font-medium">Discord</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {people.data?.items.map((person) => (
                  <tr
                    key={person.id}
                    onClick={(e) => { if (!(e.target as HTMLElement).closest('a')) navigate(personHref(person.id)); }}
                    className={`cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/50 ${person.status === 'Inactive' ? 'opacity-60' : ''}`}
                  >
                    <td className="px-5 py-2.5">
                      <span className="flex items-center gap-2.5">
                        <Avatar discordUserId={person.discordUserId} name={person.fullName} className="size-8 text-[11px]" />
                        <Link to={personHref(person.id)} className="font-medium hover:text-indigo-700 hover:underline dark:hover:text-indigo-300">{person.fullName}</Link>
                        {person.isAdmin && <Badge tone="indigo">admin</Badge>}
                        {person.status === 'Inactive' && <Badge>không hoạt động</Badge>}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 font-mono text-xs text-slate-600 dark:text-slate-400">{person.studentId}</td>
                    <td className="px-3 py-2.5">{person.department.name}</td>
                    <td className="px-3 py-2.5 tabular-nums">{person.generation}</td>
                    <td className="px-5 py-2.5">{person.discordUserId ? <Badge tone="green">đã link</Badge> : <Badge>chưa</Badge>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {people.data && people.data.total > pageSize && (
          <Pager page={page} pageSize={pageSize} total={people.data.total} onPage={(p) => setParams((prev) => { const next = new URLSearchParams(prev); next.set('page', String(p)); return next; })} />
        )}
      </Card>
    </>
  );
}
