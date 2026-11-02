import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type ImportResult, type PersonKind } from '../api';
import { useMe } from '../Layout';
import { Button, buttonClass, Card, ErrorText, Notice, useToast } from '../ui';

// Mirrors importColumns in backend/src/api/routes/people-csv.ts; clubEmail is admin-only.
const columns = ['studentId', 'fullName', 'department', 'generation', 'phone', 'personalEmail', 'birthDate', 'facebookUrl', 'clubEmail'];

export function ImportCsv({ kind }: { kind: PersonKind }) {
  const me = useMe();
  const queryClient = useQueryClient();
  const toast = useToast();
  const [csv, setCsv] = useState<string | null>(null);
  const [preview, setPreview] = useState<ImportResult | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [inputKey, setInputKey] = useState(0);

  const header = (me.isAdmin ? columns : columns.filter((column) => column !== 'clubEmail')).join(',');
  const send = (text: string, dryRun: boolean) => api<ImportResult>('/people/import', { method: 'POST', body: { kind, csv: text, dryRun } });

  const choose = async (file: File | undefined) => {
    setPreview(null);
    setError(null);
    setCsv(null);
    if (!file) return;
    setBusy(true);
    try {
      const text = await file.text();
      setPreview(await send(text, true));
      setCsv(text);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    if (!csv) return;
    setBusy(true);
    try {
      const result = await send(csv, false);
      if (result.errors?.length) {
        setPreview(result);
        return;
      }
      toast.success(`Đã thêm ${result.created} người.`);
      setCsv(null);
      setPreview(null);
      setInputKey((key) => key + 1);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['people'] }),
        queryClient.invalidateQueries({ queryKey: ['probation'] }),
      ]);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  const errors = preview?.errors ?? [];
  return (
    <Card>
      <details>
        <summary className="cursor-pointer text-sm font-medium">Nhập từ CSV</summary>
        <div className="mt-3 space-y-3 text-sm">
          <p className="text-slate-500">
            Dòng đầu là tên cột: <code className="font-mono text-xs">{header}</code>. Bốn cột đầu bắt buộc; <code>department</code> là tên ban,
            {' '}<code>birthDate</code> dạng YYYY-MM-DD. Lưu từ Excel bằng “CSV UTF-8”. Có một dòng lỗi thì không ai được thêm.
          </p>
          <div className="flex flex-wrap items-center gap-2">
            <a className={buttonClass('secondary')} download="felion-mau.csv" href={`data:text/csv;charset=utf-8,${encodeURIComponent(`﻿${header}\r\n`)}`}>Tải file mẫu</a>
            <input key={inputKey} type="file" accept=".csv,text/csv" disabled={busy} onChange={(e) => void choose(e.target.files?.[0])} aria-label="Chọn file CSV" />
          </div>
          <ErrorText error={error} />
          {preview && errors.length === 0 && (
            <div className="flex flex-wrap items-center gap-2">
              <Notice tone="green">{preview.valid} dòng hợp lệ.</Notice>
              <Button disabled={busy || !preview.valid} onClick={() => void confirm()}>Nhập {preview.valid} người</Button>
            </div>
          )}
          {errors.length > 0 && (
            <>
              <Notice>{errors.length} dòng lỗi, {preview?.valid ?? 0} dòng hợp lệ. Sửa file rồi chọn lại.</Notice>
              <ul className="max-h-60 list-inside list-disc overflow-y-auto text-rose-600">
                {errors.map((item) => <li key={`${item.row}-${item.error}`}>Dòng {item.row}: {item.error}</li>)}
              </ul>
            </>
          )}
        </div>
      </details>
    </Card>
  );
}
