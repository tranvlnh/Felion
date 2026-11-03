import { keepPreviousData, useQuery } from '@tanstack/react-query';
import DOMPurify from 'dompurify';
import { useEffect, useRef, useState, type ClipboardEvent } from 'react';
import { api, type AddressMode, type Department, type EmailJob, type EmailRecipient, type EmailRecipients } from '../api';
import { useRoles } from '../Layout';
import { Button, Card, EmptyState, ErrorText, Field, Input, Loading, Notice, PageHeader, Select, useToast } from '../ui';

const variables = ['name', 'studentId', 'email', 'department', 'generation'];
const maxPayloadBytes = 15 * 1024 * 1024;

const readDataUrl = (file: Blob) => new Promise<string>((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result as string);
  reader.onerror = () => reject(reader.error);
  reader.readAsDataURL(file);
});

const formatSize = (bytes: number) => (bytes < 1024 * 1024 ? `${Math.ceil(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`);

function JobProgress({ jobId }: { jobId: string }) {
  const job = useQuery({
    queryKey: ['email-job', jobId],
    queryFn: () => api<EmailJob>(`/email/jobs/${jobId}`),
    refetchInterval: (query) => (query.state.data?.done ? false : 2000),
  });
  if (!job.data) return <ErrorText error={job.error} />;
  const { total, sent, failed, done, stopped } = job.data;
  return (
    <Card title={done ? 'Đã gửi xong' : 'Đang gửi…'}>
      <p className="text-sm">
        Đã gửi <b className="tabular-nums">{sent}/{total}</b>
        {failed.length > 0 && <>, lỗi <b className="tabular-nums text-rose-600">{failed.length}</b></>}
      </p>
      <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
        <div className="h-full bg-indigo-600 transition-all" style={{ width: `${((sent + failed.length) / total) * 100}%` }} />
      </div>
      {stopped && <div className="mt-3"><Notice>Đã dừng sau nhiều lỗi liên tiếp, phần còn lại chưa được gửi. Kiểm tra cấu hình hoặc thử lại sau.</Notice></div>}
      {failed.length > 0 && (
        <ul className="mt-3 space-y-1 text-sm">
          {failed.map((item) => <li key={item.email}><b>{item.fullName}</b> ({item.email}): <span className="text-rose-600">{item.error}</span></li>)}
        </ul>
      )}
    </Card>
  );
}

export function EmailPage() {
  const toast = useToast();
  const roles = useRoles();
  const departments = useQuery({ queryKey: ['departments'], queryFn: () => api<Department[]>('/departments') });
  const [filter, setFilter] = useState({ kind: '', status: 'Active', departmentId: '', generation: '', roleId: '' });
  const [address, setAddress] = useState<AddressMode>('clubThenPersonal');
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  // Kept across searches and filters, so people can be picked from several searches.
  const [picked, setPicked] = useState<ReadonlyMap<string, EmailRecipient>>(new Map());
  const [subject, setSubject] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const editor = useRef<HTMLDivElement>(null);
  const imageInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const query = new URLSearchParams({ address });
  for (const [key, value] of Object.entries({ ...filter, search: debouncedSearch })) if (value) query.set(key, value);
  const recipients = useQuery({
    queryKey: ['email-recipients', query.toString()],
    queryFn: () => api<EmailRecipients>(`/email/recipients?${query}`),
    placeholderData: keepPreviousData,
  });
  const job = useQuery({ queryKey: ['email-job', jobId], queryFn: () => api<EmailJob>(`/email/jobs/${jobId}`), enabled: false });
  const sending = jobId !== null && job.data?.done !== true;

  const items = recipients.data?.items ?? [];
  const selected = [...picked.values()];
  const allShownPicked = items.length > 0 && items.every((item) => picked.has(item.id));
  const pick = (list: readonly EmailRecipient[], on: boolean) => setPicked((prev) => {
    const next = new Map(prev);
    for (const item of list) if (on) next.set(item.id, item); else next.delete(item.id);
    return next;
  });
  // The shown emails depend on the address kind, so a new kind starts a new selection.
  const changeAddress = (mode: AddressMode) => {
    setAddress(mode);
    setPicked(new Map());
  };
  const setKey = (key: keyof typeof filter) => (e: { target: { value: string } }) => setFilter({ ...filter, [key]: e.target.value });

  // execCommand is deprecated but is still the only way to insert at the caret and keep undo working.
  const insertHtml = (html: string) => {
    editor.current?.focus();
    document.execCommand('insertHTML', false, DOMPurify.sanitize(html));
  };
  const insertImages = async (images: File[]) => {
    for (const image of images) insertHtml(`<img src="${await readDataUrl(image)}" style="max-width:100%">`);
  };
  const onPaste = (e: ClipboardEvent) => {
    const html = e.clipboardData.getData('text/html');
    const images = [...e.clipboardData.files].filter((file) => file.type.startsWith('image/'));
    if (!html && images.length === 0) return;
    e.preventDefault();
    if (html) insertHtml(html); else void insertImages(images);
  };

  const send = async (test: boolean) => {
    const html = DOMPurify.sanitize(editor.current?.innerHTML ?? '');
    if (!subject.trim() || (!editor.current?.textContent?.trim() && !html.includes('<img'))) {
      toast.warn('Nhập tiêu đề và nội dung trước khi gửi.');
      return;
    }
    if (files.reduce((sum, file) => sum + file.size, 0) > maxPayloadBytes) {
      toast.warn('Tệp đính kèm tối đa 15 MB tổng cộng.');
      return;
    }
    if (!test && !window.confirm(`Gửi email "${subject.trim()}" tới ${selected.length} người?`)) return;
    setBusy(true);
    try {
      const attachments = await Promise.all(files.map(async (file) => ({
        filename: file.name,
        contentType: file.type || 'application/octet-stream',
        data: (await readDataUrl(file)).split(',')[1] ?? '',
      })));
      const body = { subject, html, address, personIds: test ? [] : selected.map((item) => item.id), attachments, test };
      if (test) {
        const result = await api<{ sentTo: string }>('/email/send', { method: 'POST', body });
        toast.success(`Đã gửi thử tới ${result.sentTo}.`);
      } else {
        setJobId((await api<{ jobId: string }>('/email/send', { method: 'POST', body })).jobId);
      }
    } catch (error) {
      toast.error(error);
    } finally {
      setBusy(false);
    }
  };

  if (recipients.isLoading) return <Loading />;
  const configured = recipients.data?.configured ?? false;

  return (
    <>
      <PageHeader
        title="Gửi email"
        subtitle={recipients.data?.sender ? <>Mỗi người nhận một email riêng, gửi từ <b>{recipients.data.sender}</b>.</> : undefined}
      />
      {!configured && (
        <Notice>Server chưa cấu hình gửi email. Đặt EMAIL_SENDER và EMAIL_APP_PASSWORD (xem README).</Notice>
      )}

      <Card title={`Người nhận (đã chọn ${selected.length})`}>
        <Input type="search" placeholder="Tìm tên, MSSV, email…" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Tìm người nhận" />
        <div className="mt-2 grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
          <Select value={filter.kind} onChange={setKey('kind')} aria-label="Loại">
            <option value="">Mọi loại</option>
            <option value="Member">Member</option>
            <option value="Candidate">Candidate</option>
          </Select>
          <Select value={filter.status} onChange={setKey('status')} aria-label="Trạng thái">
            <option value="">Mọi trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Inactive">Không hoạt động</option>
          </Select>
          <Select value={filter.departmentId} onChange={setKey('departmentId')} aria-label="Ban">
            <option value="">Mọi ban</option>
            {departments.data?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
          <Input type="number" min={1} placeholder="Gen" value={filter.generation} onChange={setKey('generation')} aria-label="Gen" />
          <Select value={filter.roleId} onChange={setKey('roleId')} aria-label="Role">
            <option value="">Mọi role</option>
            {roles.data?.filter((role) => !role.managed).map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
          </Select>
          <Select value={address} onChange={(e) => changeAddress(e.target.value as AddressMode)} aria-label="Địa chỉ gửi tới">
            <option value="clubThenPersonal">Email CLB, không có thì cá nhân</option>
            <option value="club">Chỉ email CLB</option>
            <option value="personal">Chỉ email cá nhân</option>
          </Select>
        </div>
        <ErrorText error={recipients.error} />
        {items.length === 0 ? <div className="mt-3"><EmptyState>Không có ai có email khớp tìm kiếm.</EmptyState></div> : (
          <div className="mt-3">
            <label className="flex items-center gap-2 border-b border-slate-100 pb-2 text-sm font-medium dark:border-slate-800">
              <input type="checkbox" checked={allShownPicked} onChange={(e) => pick(items, e.target.checked)} />
              Chọn tất cả kết quả ({items.length})
            </label>
            <ul className="max-h-80 divide-y divide-slate-100 overflow-y-auto text-sm dark:divide-slate-800">
              {items.map((item) => (
                <li key={item.id}>
                  <label className="flex items-center gap-2 py-1.5">
                    <input type="checkbox" checked={picked.has(item.id)} onChange={(e) => pick([item], e.target.checked)} />
                    <span className="min-w-0 flex-1 truncate"><b className="font-medium">{item.fullName}</b> <span className="text-slate-500">{item.studentId}</span></span>
                    <span className="truncate text-slate-500">{item.email}</span>
                  </label>
                </li>
              ))}
            </ul>
          </div>
        )}
        {selected.length > 0 && (
          <div className="mt-3 border-t border-slate-100 pt-3 dark:border-slate-800">
            <div className="mb-2 flex items-center justify-between gap-2 text-sm font-medium">
              Đã chọn ({selected.length})
              <Button variant="secondary" className="h-7 px-2.5 text-xs" onClick={() => setPicked(new Map())}>Bỏ chọn tất cả</Button>
            </div>
            <ul className="flex max-h-40 flex-wrap gap-1.5 overflow-y-auto">
              {selected.map((item) => (
                <li key={item.id} title={item.email} className="inline-flex items-center gap-1 rounded-md bg-indigo-50 py-0.5 pr-1 pl-2 text-xs text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-200">
                  {item.fullName}
                  <button type="button" className="rounded px-1 opacity-60 hover:opacity-100" aria-label={`Bỏ ${item.fullName}`} onClick={() => pick([item], false)}>×</button>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Card>

      <Card title="Nội dung">
        <div className="space-y-3">
          <Field label="Tiêu đề">
            <Input value={subject} maxLength={200} onChange={(e) => setSubject(e.target.value)} />
          </Field>
          <div className="space-y-1.5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-medium text-slate-700 dark:text-slate-300">Nội dung</span>
              <span className="flex flex-wrap items-center gap-1.5">
                {variables.map((name) => (
                  <button
                    key={name}
                    type="button"
                    className="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-700 hover:bg-indigo-100 dark:bg-slate-800 dark:text-slate-300"
                    title="Chèn biến"
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => insertHtml(`{{${name}}}`)}
                  >
                    {`{{${name}}}`}
                  </button>
                ))}
                <Button variant="secondary" className="h-7 px-2.5 text-xs" onClick={() => imageInput.current?.click()}>Chèn ảnh</Button>
                <input
                  ref={imageInput}
                  type="file"
                  accept="image/*"
                  multiple
                  hidden
                  onChange={(e) => { void insertImages([...(e.target.files ?? [])]); e.target.value = ''; }}
                />
              </span>
            </div>
            <div
              ref={editor}
              contentEditable
              role="textbox"
              aria-multiline
              aria-label="Nội dung email"
              onPaste={onPaste}
              className="min-h-64 overflow-auto rounded-lg bg-white px-3 py-2 text-sm shadow-xs ring-1 ring-slate-200 ring-inset focus:ring-2 focus:ring-indigo-500 focus:outline-none dark:bg-slate-900 dark:ring-slate-700 [&_img]:max-w-full"
            />
            <span className="block text-xs text-slate-500">
              Copy từ Google Docs rồi dán vào đây để giữ định dạng và hình ảnh. Biến như {'{{name}}'} được thay bằng thông tin từng người.
            </span>
          </div>
          <Field label="Tệp đính kèm" hint="Tổng ảnh và tệp tối đa 15 MB.">
            <input
              type="file"
              multiple
              className="block text-sm"
              onChange={(e) => { setFiles([...files, ...(e.target.files ?? [])]); e.target.value = ''; }}
            />
          </Field>
          {files.length > 0 && (
            <ul className="space-y-1 text-sm">
              {files.map((file, index) => (
                <li key={`${file.name}-${index}`} className="flex items-center gap-2">
                  <span className="truncate">{file.name}</span>
                  <span className="text-slate-500">{formatSize(file.size)}</span>
                  <button type="button" className="text-rose-600 hover:underline" onClick={() => setFiles(files.filter((_, i) => i !== index))}>Xoá</button>
                </li>
              ))}
            </ul>
          )}
          {configured && (
            <div className="flex flex-wrap justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button variant="secondary" disabled={busy} onClick={() => void send(true)}>Gửi thử cho tôi</Button>
              <Button disabled={busy || sending || selected.length === 0} onClick={() => void send(false)}>
                Gửi ({selected.length})
              </Button>
            </div>
          )}
        </div>
      </Card>

      {jobId && <JobProgress jobId={jobId} />}
    </>
  );
}
