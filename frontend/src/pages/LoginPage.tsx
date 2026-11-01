import { useSearchParams } from 'react-router';

const errors: Record<string, string> = {
  state: 'Phiên đăng nhập đã hết hạn, hãy thử lại.',
  google: 'Google từ chối đăng nhập, hãy thử lại.',
  domain: 'Hãy dùng tài khoản email của CLB.',
  'not-member': 'Email này chưa được admin thêm vào hệ thống, hoặc tài khoản đã bị khóa.',
};

export function LoginPage() {
  const [params] = useSearchParams();
  const error = params.get('error');

  return (
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-indigo-50 via-slate-50 to-violet-50 px-4 dark:from-slate-950 dark:via-slate-950 dark:to-indigo-950">
      <div className="w-full max-w-sm space-y-6 rounded-2xl bg-white p-8 text-center shadow-xl ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800">
        <span className="mx-auto flex size-12 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-violet-600 text-xl font-bold text-white shadow-md">F</span>
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">Felion Admin</h1>
          <p className="text-sm text-slate-500">Đăng nhập bằng email CLB</p>
        </div>
        {error && <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-800 ring-1 ring-rose-600/20 ring-inset dark:bg-rose-950 dark:text-rose-200">{errors[error] ?? 'Đăng nhập thất bại.'}</p>}
        <a href="/auth/google" className="flex h-11 items-center justify-center gap-3 rounded-lg bg-white text-sm font-medium text-slate-700 shadow-xs ring-1 ring-slate-300 ring-inset transition hover:bg-slate-50 dark:bg-slate-800 dark:text-slate-100 dark:ring-slate-700 dark:hover:bg-slate-700">
          <svg aria-hidden viewBox="0 0 24 24" className="size-5">
            <path fill="#4285F4" d="M23.5 12.3c0-.8-.1-1.6-.2-2.3H12v4.4h6.5a5.5 5.5 0 0 1-2.4 3.6v3h3.9c2.2-2.1 3.5-5.1 3.5-8.7z" />
            <path fill="#34A853" d="M12 24c3.2 0 6-1.1 8-2.9l-3.9-3c-1.1.7-2.5 1.2-4.1 1.2-3.1 0-5.8-2.1-6.7-5H1.3v3.1A12 12 0 0 0 12 24z" />
            <path fill="#FBBC05" d="M5.3 14.3a7.2 7.2 0 0 1 0-4.6V6.6h-4a12 12 0 0 0 0 10.8z" />
            <path fill="#EA4335" d="M12 4.8c1.8 0 3.4.6 4.6 1.8l3.4-3.4A12 12 0 0 0 1.3 6.6l4 3.1c.9-2.9 3.6-4.9 6.7-4.9z" />
          </svg>
          Đăng nhập bằng Google
        </a>
      </div>
    </main>
  );
}
