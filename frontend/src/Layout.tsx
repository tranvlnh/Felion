import { useQuery } from '@tanstack/react-query';
import { createContext, useContext, type ReactNode } from 'react';
import { NavLink, Outlet } from 'react-router';
import { api, can, type GuildRole, type Me } from './api';
import { Avatar, Button, ErrorText, Loading } from './ui';

const MeContext = createContext<Me | null>(null);

export function useMe(): Me {
  const me = useContext(MeContext);
  if (!me) throw new Error('useMe outside Layout');
  return me;
}

export function useRoles() {
  return useQuery({ queryKey: ['roles'], queryFn: () => api<GuildRole[]>('/roles') });
}

export function useRoleMap(): Map<string, GuildRole> {
  const { data } = useRoles();
  return new Map((data ?? []).map((role) => [role.id, role]));
}

const Icon = ({ children }: { children: ReactNode }) => (
  <svg aria-hidden viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round" className="size-[18px] shrink-0">{children}</svg>
);

const icons = {
  me: <Icon><circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" /></Icon>,
  people: <Icon><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20a6.5 6.5 0 0 1 13 0M16 4.5a3.5 3.5 0 0 1 0 7M18 14a6.5 6.5 0 0 1 3.5 6" /></Icon>,
  probation: <Icon><path d="M5 21V4h11l-2 4 2 4H5" /></Icon>,
  roles: <Icon><path d="M12 3l7 3v6c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z" /></Icon>,
  departments: <Icon><rect x="3.5" y="3.5" width="7" height="7" rx="1.5" /><rect x="13.5" y="3.5" width="7" height="7" rx="1.5" /><rect x="3.5" y="13.5" width="7" height="7" rx="1.5" /><rect x="13.5" y="13.5" width="7" height="7" rx="1.5" /></Icon>,
  settings: <Icon><path d="M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1" /><circle cx="15" cy="6" r="2" /><circle cx="9" cy="12" r="2" /><circle cx="17" cy="18" r="2" /></Icon>,
  audit: <Icon><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></Icon>,
  logout: <Icon><path d="M15 12H4M8 8l-4 4 4 4M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4" /></Icon>,
};

const Brand = () => (
  <span className="flex items-center gap-2.5">
    <span className="flex size-8 items-center justify-center rounded-lg bg-gradient-to-br from-indigo-500 to-violet-600 text-sm font-bold text-white shadow-sm">F</span>
    <span className="leading-tight">
      <span className="block font-semibold">Felion</span>
      <span className="block text-xs text-slate-500">GDG Admin</span>
    </span>
  </span>
);

async function logout() {
  await fetch('/auth/logout', { method: 'POST', headers: { 'x-felion-csrf': '1' } });
  window.location.assign('/login');
}

export function Layout() {
  const { data: me, error, isLoading } = useQuery({ queryKey: ['me'], queryFn: () => api<Me>('/me') });
  if (isLoading) return <Loading />;
  if (!me) return <div className="p-6"><ErrorText error={error} /></div>;

  const links = [
    { to: '/', label: 'Hồ sơ', icon: icons.me, show: true },
    { to: '/people', label: 'Thành viên', icon: icons.people, show: can(me, 'ViewPeople') },
    { to: '/probation', label: 'Probation', icon: icons.probation, show: can(me, 'ViewPeople') },
    { to: '/roles', label: 'Role', icon: icons.roles, show: true },
    { to: '/departments', label: 'Ban', icon: icons.departments, show: can(me, 'ManagePeople') },
    { to: '/settings', label: 'Cài đặt', icon: icons.settings, show: me.isAdmin },
    { to: '/audit', label: 'Lịch sử', icon: icons.audit, show: me.isAdmin },
  ].filter((link) => link.show);

  const nav = (className: string) => links.map((link) => (
    <NavLink
      key={link.to}
      to={link.to}
      end={link.to === '/'}
      className={({ isActive }) => `flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition ${className} ${isActive
        ? 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300'
        : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-100'}`}
    >
      {link.icon}
      {link.label}
    </NavLink>
  ));

  return (
    <MeContext.Provider value={me}>
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-60 flex-col border-r border-slate-200 bg-white lg:flex dark:border-slate-800 dark:bg-slate-900">
        <div className="px-5 py-5"><Brand /></div>
        <nav className="flex-1 space-y-0.5 overflow-y-auto px-3">{nav('')}</nav>
        <div className="flex items-center gap-2.5 border-t border-slate-200 px-4 py-3 dark:border-slate-800">
          <Avatar discordUserId={me.person.discordUserId} name={me.person.fullName} className="size-8" />
          <span className="min-w-0 flex-1 leading-tight">
            <span className="block truncate text-sm font-medium">{me.person.fullName}</span>
            <span className="block truncate text-xs text-slate-500">{me.isAdmin ? 'Admin' : me.person.department.name}</span>
          </span>
          <Button variant="secondary" className="size-8 px-0" title="Đăng xuất" aria-label="Đăng xuất" onClick={() => void logout()}>{icons.logout}</Button>
        </div>
      </aside>

      <header className="sticky top-0 z-40 border-b border-slate-200 bg-white/90 backdrop-blur lg:hidden dark:border-slate-800 dark:bg-slate-900/90">
        <div className="flex items-center justify-between gap-3 px-4 py-2.5">
          <Brand />
          <span className="flex items-center gap-2">
            <Avatar discordUserId={me.person.discordUserId} name={me.person.fullName} className="size-8" />
            <Button variant="secondary" className="size-8 px-0" title="Đăng xuất" aria-label="Đăng xuất" onClick={() => void logout()}>{icons.logout}</Button>
          </span>
        </div>
        <nav className="flex gap-1 overflow-x-auto px-3 pb-2">{nav('shrink-0 py-1.5')}</nav>
      </header>

      <main className="lg:pl-60">
        <div className="mx-auto max-w-6xl space-y-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </div>
      </main>
    </MeContext.Provider>
  );
}
