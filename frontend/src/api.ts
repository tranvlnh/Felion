export type WebPermission = 'ViewPeople' | 'ManagePeople' | 'AssignRoles' | 'KickMembers';
export const webPermissions: { value: WebPermission; label: string }[] = [
  { value: 'ViewPeople', label: 'Xem danh sách thành viên' },
  { value: 'ManagePeople', label: 'Quản lý thành viên (tạo, sửa, promote)' },
  { value: 'AssignRoles', label: 'Thêm/bớt role, sync' },
  { value: 'KickMembers', label: 'Kick khỏi server' },
];

export type PersonKind = 'Candidate' | 'Member';
export type PersonStatus = 'Active' | 'Inactive';

export type Person = {
  id: string;
  kind: PersonKind;
  status: PersonStatus;
  studentId: string;
  fullName: string;
  clubEmail: string | null;
  phone: string | null;
  personalEmail: string | null;
  // ISO timestamp at UTC midnight; the date is its first 10 chars.
  birthDate: string | null;
  facebookUrl: string | null;
  discordUserId: string | null;
  isAdmin: boolean;
  generation: number;
  team: { id: string; name: string; discordRoleId: string | null } | null;
  probationApproved: boolean;
  createdAt: string;
  updatedAt: string;
  department: { id: string; name: string };
};
export type PersonDetail = Person & { roles: string[]; mentorTeams: { id: string; name: string }[] };
export type Me = { person: PersonDetail; isAdmin: boolean; permissions: WebPermission[] };

export type GuildRole = {
  id: string;
  name: string;
  color: number;
  position: number;
  permissions: string;
  managed: boolean;
  assignable: boolean;
  memberCount: number;
  webPermissions: WebPermission[];
};

export type SyncPlan = { add: string[]; remove: string[]; missing: string[]; unmanageable: string[] };
export type SyncOutcome = { status: 'Synced' | 'Preview' | 'NotLinked' | 'Inactive' | 'NotInServer'; plan?: SyncPlan };
export type WriteResult = { person: PersonDetail; sync?: SyncOutcome | null; syncError?: string | null };

export type Department = { id: string; name: string; active: boolean };
export type TeamPerson = Pick<Person, 'id' | 'fullName' | 'studentId' | 'status' | 'discordUserId' | 'isAdmin' | 'probationApproved'> & { roles: string[] };
export type ProbationTeam = { id: string; name: string; discordRoleId: string | null; mentors: TeamPerson[]; candidates: TeamPerson[] };
export type ProbationOverview = { teams: ProbationTeam[]; unassigned: TeamPerson[] };
export type SyncErrors = { syncErrors: { fullName: string; error: string }[] };
export type Settings = {
  probationRoleId: string | null;
  memberRoleId: string | null;
  verificationChannelId: string | null;
  verificationMessageId: string | null;
  verificationTitle: string;
  verificationDescription: string;
  verificationButtonLabel: string;
};
export type Channel = { id: string; name: string };
export type AuditItem = {
  id: string;
  action: string;
  entityType: string;
  entityId: string | null;
  metadata: unknown;
  createdAt: string;
  actor: { id: string; fullName: string } | null;
};
export type SyncAllReport = {
  checked: number;
  changed: { personId: string; fullName: string; add: string[]; remove: string[] }[];
  notInServer: number;
  failed: { personId: string; fullName: string; error: string }[];
};
export type Page<T> = { items: T[]; total: number };

export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
  }
}

export async function api<T>(path: string, options: { method?: string; body?: unknown } = {}): Promise<T> {
  const hasBody = options.body !== undefined;
  const response = await fetch(`/api${path}`, {
    method: options.method ?? 'GET',
    credentials: 'same-origin',
    headers: { 'x-felion-csrf': '1', ...(hasBody ? { 'content-type': 'application/json' } : {}) },
    ...(hasBody ? { body: JSON.stringify(options.body) } : {}),
  });
  if (response.status === 401 && window.location.pathname !== '/login') {
    window.location.assign('/login');
  }
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new ApiError(response.status, (data as { error?: string } | null)?.error ?? response.statusText);
  }
  return data as T;
}

export function can(me: Me, permission: WebPermission): boolean {
  return me.isAdmin || me.permissions.includes(permission);
}

// Mirrors the server rule; the server is the one that enforces it.
const dangerousBits = (1n << 3n) | (1n << 5n) | (1n << 28n) | (1n << 2n) | (1n << 1n) | (1n << 4n) | (1n << 29n);
export function isPrivilegedRole(role: GuildRole): boolean {
  return role.webPermissions.length > 0 || (BigInt(role.permissions) & dangerousBits) !== 0n;
}

export const discordPermissionFlags: { bit: bigint; label: string; danger?: boolean }[] = [
  { bit: 1n << 3n, label: 'Administrator', danger: true },
  { bit: 1n << 5n, label: 'Manage Server', danger: true },
  { bit: 1n << 28n, label: 'Manage Roles', danger: true },
  { bit: 1n << 4n, label: 'Manage Channels', danger: true },
  { bit: 1n << 29n, label: 'Manage Webhooks', danger: true },
  { bit: 1n << 1n, label: 'Kick Members', danger: true },
  { bit: 1n << 2n, label: 'Ban Members', danger: true },
  { bit: 1n << 40n, label: 'Timeout Members' },
  { bit: 1n << 13n, label: 'Manage Messages' },
  { bit: 1n << 34n, label: 'Manage Threads' },
  { bit: 1n << 27n, label: 'Manage Nicknames' },
  { bit: 1n << 33n, label: 'Manage Events' },
  { bit: 1n << 17n, label: 'Mention @everyone' },
  { bit: 1n << 22n, label: 'Mute Members' },
  { bit: 1n << 24n, label: 'Move Members' },
  { bit: 1n << 10n, label: 'View Channels' },
  { bit: 1n << 11n, label: 'Send Messages' },
  { bit: 1n << 16n, label: 'Read Message History' },
  { bit: 1n << 14n, label: 'Embed Links' },
  { bit: 1n << 15n, label: 'Attach Files' },
  { bit: 1n << 6n, label: 'Add Reactions' },
  { bit: 1n << 18n, label: 'Use External Emoji' },
  { bit: 1n << 20n, label: 'Connect (voice)' },
  { bit: 1n << 21n, label: 'Speak (voice)' },
  { bit: 1n << 0n, label: 'Create Invite' },
  { bit: 1n << 26n, label: 'Change Nickname' },
];
