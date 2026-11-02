import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { api, can, isPrivilegedRole, type GuildRole, type Page, type Person, type ProbationOverview, type ProbationTeam, type SyncErrors, type TeamPerson } from '../api';
import { useMe, useRoleMap, useRoles } from '../Layout';
import { Avatar, Badge, Button, buttonClass, Card, EmptyState, ErrorText, Input, Loading, PageHeader, RoleChip, Select, useToast } from '../ui';

type Run = (action: () => Promise<Partial<SyncErrors>>, success: string) => Promise<void>;
type Ctx = { teams: ProbationTeam[]; canManage: boolean; busy: boolean; run: Run; canSelect: boolean; selected: Set<string>; select: (ids: string[], on: boolean) => void };

const endConfirmWord = 'KET THUC';
const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

export function ProbationPage() {
  const me = useMe();
  const queryClient = useQueryClient();
  const roles = useRoles();
  const toast = useToast();
  const overview = useQuery({ queryKey: ['probation'], queryFn: () => api<ProbationOverview>('/probation') });
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState('');
  const [roleId, setRoleId] = useState('');
  const [filter, setFilter] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [target, setTarget] = useState('');

  if (overview.isLoading) return <Loading />;
  if (!overview.data) return <ErrorText error={overview.error} />;
  const { teams, unassigned } = overview.data;
  const canManage = can(me, 'ManagePeople');
  const canKick = can(me, 'KickMembers');
  const canSelect = canManage || canKick;

  const run: Run = async (action, success) => {
    setBusy(true);
    try {
      const failed = (await action()).syncErrors ?? [];
      if (failed.length > 0) toast.warn(`${success} Nhưng chưa đồng bộ được Discord cho: ${failed.map((f) => `${f.fullName} (${f.error})`).join('; ')}`);
      else toast.success(success);
    } catch (e) {
      toast.error(e);
    } finally {
      // Also after a failure: a bulk move may have saved some people before it stopped.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['probation'] }),
        queryClient.invalidateQueries({ queryKey: ['people'] }),
        queryClient.invalidateQueries({ queryKey: ['person'] }),
      ]);
      setBusy(false);
    }
  };
  const select = (ids: string[], on: boolean) => setSelected((prev) => {
    const next = new Set(prev);
    for (const id of ids) {
      if (on) next.add(id);
      else next.delete(id);
    }
    return next;
  });
  const ctx: Ctx = { teams, canManage, busy, run, canSelect, selected, select };

  const usedRoleIds = new Set(teams.flatMap((team) => (team.discordRoleId ? [team.discordRoleId] : [])));
  // Same filter as the server: assignable, and non-admins only get roles without power.
  const roleOptions = (current: string | null) => (roles.data ?? []).filter((role) =>
    role.id === current || (role.assignable && !usedRoleIds.has(role.id) && (me.isAdmin || !isPrivilegedRole(role))));

  const all = [...unassigned, ...teams.flatMap((team) => team.candidates)];
  const approved = all.filter((candidate) => candidate.probationApproved).length;
  const term = fold(filter.trim());
  const matches = (person: TeamPerson) => !term || fold(person.fullName).includes(term) || fold(person.studentId).includes(term);
  const visibleTeams = teams
    .map((team) => (!term || fold(team.name).includes(term) ? team : { ...team, candidates: team.candidates.filter(matches) }))
    .filter((team) => !term || fold(team.name).includes(term) || team.candidates.length > 0 || team.mentors.some(matches));
  const visibleUnassigned = unassigned.filter(matches);

  const teamOf = new Map<string, string | null>([
    ...unassigned.map((candidate) => [candidate.id, null] as const),
    ...teams.flatMap((team) => team.candidates.map((candidate) => [candidate.id, team.id] as const)),
  ]);
  const picked = all.filter((candidate) => selected.has(candidate.id));
  const pickedActive = picked.filter((candidate) => candidate.status === 'Active');
  const bulk = (items: TeamPerson[], call: (candidate: TeamPerson) => Promise<Partial<SyncErrors>>, success: string) => void run(async () => {
    const syncErrors: SyncErrors['syncErrors'] = [];
    for (const candidate of items) syncErrors.push(...((await call(candidate)).syncErrors ?? []));
    setSelected(new Set());
    setTarget('');
    return { syncErrors };
  }, success);
  const moveSelected = () => {
    const teamId = target === 'none' ? null : target;
    const label = teams.find((team) => team.id === teamId)?.name ?? 'Chưa có team';
    const moving = picked.filter((candidate) => teamOf.get(candidate.id) !== teamId);
    bulk(moving, (candidate) => api(`/probation/candidates/${candidate.id}`, { method: 'PUT', body: { teamId } }), `Đã chuyển ${moving.length} người vào “${label}”.`);
  };
  const approveSelected = (approved: boolean) => {
    const items = pickedActive.filter((candidate) => candidate.probationApproved !== approved);
    bulk(items, (candidate) => api(`/probation/candidates/${candidate.id}/approval`, { method: 'PUT', body: { approved } }),
      `Đã ${approved ? 'duyệt' : 'bỏ duyệt'} ${items.length} người.`);
  };
  const kickSelected = () => {
    const reason = prompt(`Kick ${pickedActive.length} người khỏi Discord? Họ cũng chuyển sang không hoạt động.\n\nLý do:`)?.trim();
    if (reason) bulk(pickedActive, (candidate) => api(`/people/${candidate.id}/kick`, { method: 'POST', body: { reason } }), `Đã kick và chuyển ${pickedActive.length} người sang không hoạt động.`);
  };

  const endRound = () => {
    const removed = all.length - approved;
    const typed = prompt(`Kết thúc đợt probation:\n• ${approved} người đã duyệt lên thành viên (gỡ role Probation và role team, thêm role Member)\n• ${removed} người chưa duyệt bị xóa vĩnh viễn và gỡ role trên Discord\n• ${teams.length} team bị xóa\n\nKhông hoàn tác được. Gõ "${endConfirmWord}" để xác nhận.`);
    if (typed?.trim().toUpperCase() === endConfirmWord) {
      void run(() => api('/probation/end', { method: 'POST' }), `Đã kết thúc đợt: ${approved} người lên thành viên, ${removed} người bị xóa.`);
    }
  };

  return (
    <>
      <PageHeader
        title="Probation"
        subtitle="Duyệt chỉ là đánh dấu; người đã duyệt lên thành viên khi admin kết thúc đợt, người còn lại bị xóa."
        actions={
          <>
            {canManage && <Link to="/people/new?kind=Candidate" className={buttonClass()}>+ Thêm probation</Link>}
            {me.isAdmin && <Button variant="danger" disabled={busy || (all.length === 0 && teams.length === 0)} onClick={endRound}>Kết thúc đợt</Button>}
          </>
        }
      />

      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Probation" value={all.length} />
        <Stat label="Đã duyệt" value={approved} tone="text-emerald-700 dark:text-emerald-400" />
        <Stat label="Chưa duyệt" value={all.length - approved} tone="text-amber-700 dark:text-amber-400" />
        <Stat label="Team" value={teams.length} />
      </dl>

      <Card>
        <div className="flex flex-wrap gap-2">
          <Input type="search" className="sm:max-w-xs" placeholder="Lọc theo tên, MSSV, team…" value={filter} onChange={(e) => setFilter(e.target.value)} aria-label="Lọc probation" />
          {canManage && (
            <form
              className="flex flex-1 flex-wrap justify-end gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                void run(async () => {
                  await api('/probation/teams', { method: 'POST', body: { name: name.trim(), discordRoleId: roleId || null } });
                  setName('');
                  setRoleId('');
                  return {};
                }, 'Đã tạo team.');
              }}
            >
              <Input required className="sm:max-w-48" placeholder="Tên team mới" value={name} onChange={(e) => setName(e.target.value)} aria-label="Tên team mới" />
              <Select className="sm:max-w-48" value={roleId} onChange={(e) => setRoleId(e.target.value)} aria-label="Role của team mới">
                <option value="">Không gắn role</option>
                {roleOptions(null).map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
              </Select>
              <Button type="submit" disabled={busy || !name.trim()}>Tạo team</Button>
            </form>
          )}
        </div>
      </Card>

      {teams.length === 0 && <EmptyState>Chưa có team nào{canManage ? ' — tạo team ở ô phía trên.' : '.'}</EmptyState>}
      {term && visibleTeams.length === 0 && visibleUnassigned.length === 0 && <EmptyState>Không có ai khớp “{filter.trim()}”.</EmptyState>}
      {visibleTeams.map((team) => <TeamCard key={team.id} team={team} roleOptions={roleOptions(team.discordRoleId)} ctx={ctx} />)}

      {(visibleUnassigned.length > 0 || (!term && unassigned.length === 0 && all.length > 0)) && (
        <Card title={`Chưa có team (${unassigned.length})`}>
          {visibleUnassigned.length === 0
            ? <p className="text-sm text-slate-500">Mọi probation đều đã có team.</p>
            : <CandidateList candidates={visibleUnassigned} teamId={null} ctx={ctx} />}
        </Card>
      )}

      {canSelect && picked.length > 0 && (
        <div className="sticky bottom-4 z-20 flex flex-wrap items-center gap-2 rounded-xl bg-white px-4 py-3 shadow-lg ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-700">
          <span className="text-sm font-medium">Đã chọn {picked.length} probation</span>
          {canManage && (
            <>
              <Select className="sm:max-w-56" value={target} disabled={busy} onChange={(e) => setTarget(e.target.value)} aria-label="Team đích">
                <option value="" disabled>Chuyển vào team…</option>
                {teams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}
                <option value="none">Chưa có team</option>
              </Select>
              <Button disabled={busy || !target} onClick={moveSelected}>Chuyển</Button>
              <Button variant="secondary" disabled={busy || !pickedActive.some((c) => !c.probationApproved)} onClick={() => approveSelected(true)}>Duyệt</Button>
              <Button variant="secondary" disabled={busy || !pickedActive.some((c) => c.probationApproved)} onClick={() => approveSelected(false)}>Bỏ duyệt</Button>
            </>
          )}
          {canKick && <Button variant="danger" disabled={busy || pickedActive.length === 0} onClick={kickSelected}>Kick</Button>}
          <Button variant="secondary" className="ml-auto" disabled={busy} onClick={() => setSelected(new Set())}>Bỏ chọn</Button>
        </div>
      )}
    </>
  );
}

function Stat({ label, value, tone = '' }: { label: string; value: number; tone?: string }) {
  return (
    <div className="rounded-xl bg-white px-5 py-4 shadow-xs ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800">
      <dt className="text-sm text-slate-500">{label}</dt>
      <dd className={`mt-1 text-3xl font-semibold tracking-tight tabular-nums ${tone}`}>{value}</dd>
    </div>
  );
}

function TeamCard({ team, roleOptions, ctx }: { team: ProbationTeam; roleOptions: GuildRole[]; ctx: Ctx }) {
  const { canManage, busy, run } = ctx;
  const roleMap = useRoleMap();
  const [renaming, setRenaming] = useState<string | null>(null);
  const path = `/probation/teams/${team.id}`;
  const approved = team.candidates.filter((candidate) => candidate.probationApproved).length;

  return (
    <details open className="group rounded-xl bg-white shadow-xs ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800">
      <summary className="flex cursor-pointer list-none flex-wrap items-center gap-x-3 gap-y-1 rounded-xl px-5 py-3.5 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 [&::-webkit-details-marker]:hidden">
        <span aria-hidden className="text-slate-400 transition-transform group-open:rotate-90">▸</span>
        <span className="font-semibold">{team.name}</span>
        {team.discordRoleId && <RoleChip roleId={team.discordRoleId} role={roleMap.get(team.discordRoleId)} />}
        <Badge tone={team.candidates.length > 0 && approved === team.candidates.length ? 'green' : 'slate'}>duyệt {approved}/{team.candidates.length}</Badge>
        <span className="min-w-0 truncate text-sm text-slate-500">
          Mentor: {team.mentors.length > 0 ? team.mentors.map((mentor) => mentor.fullName).join(', ') : 'chưa có'}
        </span>
      </summary>

      <div className="space-y-5 border-t border-slate-100 px-5 py-4 dark:border-slate-800">
        <section className="space-y-2">
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Mentor</h3>
          <div className="flex flex-wrap gap-2">
            {team.mentors.map((mentor) => (
              <span key={mentor.id} className="inline-flex h-9 items-center gap-1.5 rounded-full bg-white py-0.5 pl-1 pr-3 text-sm ring-1 ring-slate-200 ring-inset dark:bg-slate-900 dark:ring-slate-700">
                <Avatar discordUserId={mentor.discordUserId} name={mentor.fullName} className="size-6 text-[10px]" />
                <Link to={`/people/${mentor.id}`} className="hover:underline">{mentor.fullName}</Link>
                {canManage && (
                  <button type="button" disabled={busy} className="text-slate-400 hover:text-rose-600" aria-label={`Bỏ mentor ${mentor.fullName}`}
                    onClick={() => void run(() => api(`${path}/mentors/${mentor.id}`, { method: 'DELETE' }), `Đã bỏ mentor ${mentor.fullName}.`)}>
                    ×
                  </button>
                )}
              </span>
            ))}
            {canManage && <MentorPicker exclude={team.mentors} busy={busy} onPick={(person) => void run(() => api(`${path}/mentors/${person.id}`, { method: 'PUT' }), `Đã thêm mentor ${person.fullName}.`)} />}
            {!canManage && team.mentors.length === 0 && <span className="text-sm text-slate-500">Chưa có mentor.</span>}
          </div>
        </section>

        <section className="space-y-1">
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Probation ({team.candidates.length})</h3>
          {team.candidates.length === 0
            ? <p className="text-sm text-slate-500">Chưa có probation. Chuyển người từ mục “Chưa có team”.</p>
            : <CandidateList candidates={team.candidates} teamId={team.id} ctx={ctx} />}
        </section>

        {canManage && (
          <div className="flex flex-wrap items-center gap-2 border-t border-slate-100 pt-4 dark:border-slate-800">
            {renaming === null ? (
              <Button variant="secondary" disabled={busy} onClick={() => setRenaming(team.name)}>Đổi tên</Button>
            ) : (
              <form className="flex gap-2" onSubmit={(e) => {
                e.preventDefault();
                const next = renaming.trim();
                if (next && next !== team.name) void run(() => api(path, { method: 'PATCH', body: { name: next } }), 'Đã đổi tên team.').then(() => setRenaming(null));
                else setRenaming(null);
              }}>
                <div className="w-48"><Input autoFocus value={renaming} onChange={(e) => setRenaming(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') setRenaming(null); }} aria-label="Tên mới của team" /></div>
                <Button type="submit" disabled={busy}>Lưu</Button>
              </form>
            )}
            <div className="w-56"><Select
              value={team.discordRoleId ?? ''}
              disabled={busy}
              aria-label={`Role của team ${team.name}`}
              onChange={(e) => void run(() => api(path, { method: 'PATCH', body: { discordRoleId: e.target.value || null } }), 'Đã đổi role của team.')}
            >
              <option value="">Không gắn role</option>
              {roleOptions.map((role) => <option key={role.id} value={role.id}>Role: {role.name}</option>)}
            </Select></div>
            <Button variant="danger" className="ml-auto" disabled={busy} onClick={() => {
              if (confirm(`Xóa team ${team.name}? Probation của team về “Chưa có team”, role team bị gỡ.`)) {
                void run(() => api(path, { method: 'DELETE' }), 'Đã xóa team.');
              }
            }}>Xóa team</Button>
          </div>
        )}
      </div>
    </details>
  );
}

function CandidateList({ candidates, teamId, ctx }: { candidates: TeamPerson[]; teamId: string | null; ctx: Ctx }) {
  const { teams, canManage, busy, run, canSelect, selected, select } = ctx;
  const roleMap = useRoleMap();
  const ids = candidates.map((candidate) => candidate.id);
  return (
    <>
      {canSelect && (
        <label className="flex w-fit cursor-pointer items-center gap-2 py-1 text-xs text-slate-500">
          <input type="checkbox" className="size-4" checked={ids.every((id) => selected.has(id))} onChange={(e) => select(ids, e.target.checked)} />
          Chọn tất cả ({ids.length})
        </label>
      )}
    <ul className="divide-y divide-slate-100 text-sm dark:divide-slate-800">
      {candidates.map((candidate) => {
        const active = candidate.status === 'Active';
        return (
          <li key={candidate.id} className={`flex flex-wrap items-center gap-x-3 gap-y-2 py-2 ${active ? '' : 'opacity-60'}`}>
            <span className="flex min-w-0 basis-full items-center gap-2 sm:basis-0 sm:flex-1">
              {canSelect && (
                <input type="checkbox" className="size-4 shrink-0" checked={selected.has(candidate.id)} onChange={(e) => select([candidate.id], e.target.checked)} aria-label={`Chọn ${candidate.fullName}`} />
              )}
              <Avatar discordUserId={candidate.discordUserId} name={candidate.fullName} className="size-7 shrink-0 text-[10px]" />
              <span className="min-w-0 space-y-1">
                <span className="flex flex-wrap items-center gap-2">
                  <Link to={`/people/${candidate.id}`} className="truncate font-medium hover:text-indigo-700 hover:underline dark:hover:text-indigo-300">{candidate.fullName}</Link>
                  <span className="font-mono text-xs text-slate-500">{candidate.studentId}</span>
                  {!active && <Badge>không hoạt động</Badge>}
                  {!candidate.discordUserId && <Badge>chưa link</Badge>}
                </span>
                {candidate.roles.length > 0 && (
                  <span className="flex flex-wrap gap-1">
                    {candidate.roles.map((roleId) => <RoleChip key={roleId} roleId={roleId} role={roleMap.get(roleId)} />)}
                  </span>
                )}
              </span>
            </span>
            {canManage && active ? (
              <ApproveToggle
                approved={candidate.probationApproved}
                disabled={busy}
                onToggle={() => void run(
                  () => api(`/probation/candidates/${candidate.id}/approval`, { method: 'PUT', body: { approved: !candidate.probationApproved } }),
                  candidate.probationApproved ? `Đã bỏ duyệt ${candidate.fullName}.` : `Đã duyệt ${candidate.fullName}.`,
                )}
              />
            ) : candidate.probationApproved && <Badge tone="green">đã duyệt</Badge>}
            {canManage && (
              <div className="w-40 shrink-0"><Select
                value={teamId ?? ''}
                disabled={busy}
                aria-label={`Team của ${candidate.fullName}`}
                onChange={(e) => void run(
                  () => api(`/probation/candidates/${candidate.id}`, { method: 'PUT', body: { teamId: e.target.value || null } }),
                  `Đã chuyển team cho ${candidate.fullName}.`,
                )}
              >
                <option value="">Chưa có team</option>
                {teams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}
              </Select></div>
            )}
          </li>
        );
      })}
    </ul>
    </>
  );
}

function ApproveToggle({ approved, disabled, onToggle }: { approved: boolean; disabled: boolean; onToggle: () => void }) {
  return (
    <button
      type="button"
      aria-pressed={approved}
      disabled={disabled}
      onClick={onToggle}
      title={approved ? 'Bấm để bỏ duyệt' : 'Lên thành viên khi kết thúc đợt'}
      className={`inline-flex h-9 w-28 items-center justify-center rounded-lg text-sm font-medium ring-1 ring-inset transition disabled:opacity-50 ${approved
        ? 'bg-emerald-50 text-emerald-700 ring-emerald-600/30 hover:bg-emerald-100 dark:bg-emerald-950 dark:text-emerald-300 dark:ring-emerald-400/30'
        : 'bg-white text-slate-700 ring-slate-200 hover:bg-slate-50 dark:bg-slate-900 dark:text-slate-300 dark:ring-slate-700 dark:hover:bg-slate-800'}`}
    >
      {approved ? '✓ Đã duyệt' : 'Duyệt'}
    </button>
  );
}

function MentorPicker({ exclude, busy, onPick }: { exclude: TeamPerson[]; busy: boolean; onPick: (person: Person) => void }) {
  const [search, setSearch] = useState('');
  const term = search.trim();
  const results = useQuery({
    queryKey: ['people', 'mentor-search', term],
    queryFn: () => api<Page<Person>>(`/people?${new URLSearchParams({ kind: 'Member', status: 'Active', search: term, pageSize: '8' })}`),
    enabled: term.length >= 2,
  });
  const taken = new Set(exclude.map((person) => person.id));
  const options = results.data?.items.filter((person) => !taken.has(person.id)) ?? [];

  return (
    <div className="relative w-56">
      <Input placeholder="+ Thêm mentor (gõ tên)" value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') setSearch(''); }} aria-label="Tìm thành viên làm mentor" />
      {term.length >= 2 && (
        <ul className="absolute z-10 mt-1 w-72 overflow-hidden rounded-lg bg-white py-1 text-sm shadow-lg ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-700">
          {results.isLoading && <li className="px-3 py-2 text-slate-500">Đang tìm…</li>}
          {results.data && options.length === 0 && <li className="px-3 py-2 text-slate-500">Không tìm thấy.</li>}
          {options.map((person) => (
            <li key={person.id}>
              <button type="button" disabled={busy} className="flex w-full items-center gap-2 px-3 py-1.5 text-left hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => { onPick(person); setSearch(''); }}>
                <Avatar discordUserId={person.discordUserId} name={person.fullName} className="size-6 text-[10px]" />
                <span className="flex-1">{person.fullName}</span>
                <span className="font-mono text-xs text-slate-500">{person.studentId}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
