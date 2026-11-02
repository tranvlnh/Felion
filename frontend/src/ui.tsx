import { createContext, useCallback, useContext, useRef, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import type { GuildRole } from './api';

const cx = (...classes: (string | false | undefined)[]) => classes.filter(Boolean).join(' ');

type Variant = 'primary' | 'secondary' | 'danger';

// Shared so router <Link>s can look like buttons.
export function buttonClass(variant: Variant = 'primary', className?: string) {
  return cx(
    'inline-flex h-9 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg px-3.5 text-sm font-medium shadow-xs transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:opacity-50',
    variant === 'primary' && 'bg-indigo-600 text-white hover:bg-indigo-500',
    variant === 'secondary' && 'bg-white text-slate-700 ring-1 ring-slate-200 ring-inset hover:bg-slate-50 dark:bg-slate-900 dark:text-slate-200 dark:ring-slate-700 dark:hover:bg-slate-800',
    variant === 'danger' && 'bg-rose-600 text-white hover:bg-rose-500',
    className,
  );
}

export function Button({ variant = 'primary', className, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return <button {...props} className={buttonClass(variant, className)} />;
}

const fieldClass = 'h-9 w-full rounded-lg border-0 bg-white px-3 text-sm shadow-xs ring-1 ring-slate-200 ring-inset transition placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-500 focus:outline-none disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500 dark:bg-slate-900 dark:ring-slate-700 dark:disabled:bg-slate-800/50';

// ISO date or timestamp → dd/MM/yyyy, read from the string so time zones can't shift the day.
export function formatDate(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/');
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx(fieldClass, props.className)} />;
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={cx(fieldClass, 'h-auto py-2', props.className)} />;
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={cx(fieldClass, props.className)} />;
}

export function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return (
    <label className="block space-y-1.5">
      <span className="text-sm font-medium text-slate-700 dark:text-slate-300">{label}</span>
      {children}
      {hint && <span className="block text-xs text-slate-500">{hint}</span>}
    </label>
  );
}

export function Card({ title, actions, children, className }: { title?: string; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={cx('rounded-xl bg-white shadow-xs ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800', className)}>
      {(title || actions) && (
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-3 dark:border-slate-800">
          {title && <h2 className="font-semibold">{title}</h2>}
          {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
        </div>
      )}
      <div className="p-5">{children}</div>
    </section>
  );
}

export function Badge({ children, tone = 'slate' }: { children: ReactNode; tone?: 'slate' | 'green' | 'amber' | 'red' | 'indigo' }) {
  const tones = {
    slate: 'bg-slate-50 text-slate-600 ring-slate-500/20 dark:bg-slate-800 dark:text-slate-300 dark:ring-slate-400/20',
    green: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-950 dark:text-emerald-300 dark:ring-emerald-400/20',
    amber: 'bg-amber-50 text-amber-800 ring-amber-600/20 dark:bg-amber-950 dark:text-amber-300 dark:ring-amber-400/20',
    red: 'bg-rose-50 text-rose-700 ring-rose-600/20 dark:bg-rose-950 dark:text-rose-300 dark:ring-rose-400/20',
    indigo: 'bg-indigo-50 text-indigo-700 ring-indigo-600/20 dark:bg-indigo-950 dark:text-indigo-300 dark:ring-indigo-400/20',
  };
  return <span className={cx('inline-flex items-center whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset', tones[tone])}>{children}</span>;
}

export function RoleChip({ role, roleId, onRemove }: { role?: GuildRole | undefined; roleId: string; onRemove?: (() => void) | undefined }) {
  const color = role && role.color ? `#${role.color.toString(16).padStart(6, '0')}` : '#94a3b8';
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-white px-2.5 py-1 text-xs font-medium ring-1 ring-slate-200 ring-inset dark:bg-slate-900 dark:ring-slate-700">
      <span className="size-2.5 rounded-full" style={{ backgroundColor: color }} />
      {role?.name ?? <span className="text-slate-500">role đã xóa ({roleId})</span>}
      {onRemove && (
        <button type="button" onClick={onRemove} className="text-slate-400 hover:text-rose-600" aria-label={`Gỡ role ${role?.name ?? roleId}`}>
          ×
        </button>
      )}
    </span>
  );
}

export function ErrorText({ error }: { error: unknown }) {
  if (!error) return null;
  return <p className="text-sm text-rose-600">{error instanceof Error ? error.message : String(error)}</p>;
}

export function Notice({ children, tone = 'amber' }: { children: ReactNode; tone?: 'amber' | 'green' }) {
  return (
    <p className={cx('rounded-lg px-3 py-2 text-sm ring-1 ring-inset', tone === 'amber' ? 'bg-amber-50 text-amber-900 ring-amber-600/20 dark:bg-amber-950 dark:text-amber-200' : 'bg-emerald-50 text-emerald-900 ring-emerald-600/20 dark:bg-emerald-950 dark:text-emerald-200')}>
      {children}
    </p>
  );
}

export function Loading() {
  return (
    <p role="status" className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
      <span aria-hidden className="size-4 animate-spin rounded-full border-2 border-slate-300 border-t-indigo-600" />
      Đang tải…
    </p>
  );
}

export function Pager({ page, pageSize, total, onPage }: { page: number; pageSize: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-4 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-400">
      <span>{total} kết quả</span>
      <div className="flex items-center gap-2">
        <Button variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Trước</Button>
        <span className="tabular-nums">{page}/{pages}</span>
        <Button variant="secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>Sau</Button>
      </div>
    </div>
  );
}

export function Avatar({ discordUserId, name, className = 'size-8' }: { discordUserId: string | null; name: string; className?: string }) {
  const [failed, setFailed] = useState(false);
  if (discordUserId && !failed) {
    return <img src={`/api/avatars/${discordUserId}`} alt="" loading="lazy" onError={() => setFailed(true)} className={cx('shrink-0 rounded-full object-cover', className)} />;
  }
  return (
    <span aria-hidden className={cx('inline-flex shrink-0 items-center justify-center rounded-full bg-indigo-100 text-xs font-semibold text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300', className)}>
      {name.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase()}
    </span>
  );
}

export function PageHeader({ title, subtitle, actions }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-3 pb-1">
      <div className="min-w-0">
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 max-w-2xl text-sm text-slate-500">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <p className="rounded-xl border border-dashed border-slate-300 px-4 py-10 text-center text-sm text-slate-500 dark:border-slate-700">{children}</p>;
}

type ToastTone = 'green' | 'amber' | 'red';
type Toast = { id: number; tone: ToastTone; text: string };
const ToastContext = createContext<(tone: ToastTone, text: string) => void>(() => undefined);

// Shows the result of an action; errors stay longer so they can be read.
export function useToast() {
  const push = useContext(ToastContext);
  return {
    success: (text: string) => push('green', text),
    warn: (text: string) => push('amber', text),
    error: (error: unknown) => push('red', error instanceof Error ? error.message : String(error)),
  };
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);
  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((toast) => toast.id !== id)), []);
  const push = useCallback((tone: ToastTone, text: string) => {
    const id = nextId.current++;
    setToasts((list) => [...list.slice(-3), { id, tone, text }]);
    setTimeout(() => dismiss(id), tone === 'green' ? 4000 : 8000);
  }, [dismiss]);
  const tones = {
    green: 'border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-100',
    amber: 'border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100',
    red: 'border-rose-200 bg-rose-50 text-rose-900 dark:border-rose-900 dark:bg-rose-950 dark:text-rose-100',
  };

  return (
    <ToastContext.Provider value={push}>
      {children}
      <div aria-live="polite" className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2">
        {toasts.map((toast) => (
          <div key={toast.id} role={toast.tone === 'red' ? 'alert' : 'status'} className={cx('pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border px-4 py-3 text-sm shadow-lg', tones[toast.tone])}>
            <span className="flex-1">{toast.text}</span>
            <button type="button" onClick={() => dismiss(toast.id)} className="opacity-60 hover:opacity-100" aria-label="Đóng thông báo">×</button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
