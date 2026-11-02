import { webPermissions } from '../api';
import { useMe, useRoleMap } from '../Layout';
import { Avatar, Badge, Card, formatDate, RoleChip } from '../ui';

export function MePage() {
  const me = useMe();
  const roles = useRoleMap();
  const { person } = me;

  return (
    <>
      <div className="flex items-center gap-4">
        <Avatar discordUserId={person.discordUserId} name={person.fullName} className="size-14 text-lg" />
        <div>
          <p className="text-sm text-slate-500">Xin chào 👋</p>
          <h1 className="text-2xl font-semibold tracking-tight">{person.fullName}</h1>
        </div>
      </div>
      <div className="grid items-start gap-6 lg:grid-cols-3">
      <Card title="Hồ sơ" className="lg:col-span-2">
        <dl className="grid gap-x-6 gap-y-4 text-sm sm:grid-cols-2">
          <Item label="MSSV" value={person.studentId} />
          <Item label="Email CLB" value={person.clubEmail ?? '—'} />
          <Item label="Email cá nhân" value={person.personalEmail ?? '—'} />
          <Item label="Số điện thoại" value={person.phone ?? '—'} />
          <Item label="Ngày sinh" value={person.birthDate ? formatDate(person.birthDate) : '—'} />
          <Item label="Facebook" value={person.facebookUrl ?? '—'} />
          <Item label="Ban" value={person.department.name} />
          <Item label="Gen" value={`Gen ${person.generation}`} />
          <Item label="Loại" value={person.kind === 'Member' ? 'Thành viên' : 'Probation'} />
          {person.kind === 'Candidate' && <Item label="Team" value={person.team?.name ?? 'chưa có'} />}
          {person.mentorTeams.length > 0 && <Item label="Mentor team" value={person.mentorTeams.map((team) => team.name).join(', ')} />}
          <Item label="Discord" value={person.discordUserId ?? 'chưa liên kết (bấm nút xác minh trên Discord)'} />
        </dl>
      </Card>
      <div className="space-y-6">
      <Card title="Role">
        <div className="flex flex-wrap gap-2">
          {person.roles.length === 0 && <span className="text-sm text-slate-500">Chưa có role nào.</span>}
          {person.roles.map((roleId) => <RoleChip key={roleId} roleId={roleId} role={roles.get(roleId)} />)}
        </div>
      </Card>
      <Card title="Quyền trên web">
        <div className="flex flex-wrap gap-2">
          {me.isAdmin && <Badge tone="indigo">Admin (toàn quyền)</Badge>}
          {!me.isAdmin && me.permissions.length === 0 && <span className="text-sm text-slate-500">Bạn chỉ xem được hồ sơ của mình.</span>}
          {!me.isAdmin && webPermissions.filter((p) => me.permissions.includes(p.value)).map((p) => <Badge key={p.value}>{p.label}</Badge>)}
        </div>
      </Card>
      </div>
      </div>
    </>
  );
}

function Item({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-1 font-medium break-words">{value}</dd>
    </div>
  );
}
