import { useQuery } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api, type Department, type PersonDetail, type PersonKind } from '../api';
import { useMe } from '../Layout';
import { Button, ErrorText, Field, formatDate, Input, Select } from '../ui';

export type PersonFormValues = {
  kind: PersonKind;
  studentId: string;
  fullName: string;
  departmentId: string;
  generation: string;
  clubEmail: string;
  phone: string;
  personalEmail: string;
  birthDate: string;
  facebookUrl: string;
  discordUserId: string;
  isAdmin: boolean;
};

export function toFormValues(person?: PersonDetail): PersonFormValues {
  return {
    kind: person?.kind ?? 'Candidate',
    studentId: person?.studentId ?? '',
    fullName: person?.fullName ?? '',
    departmentId: person?.department.id ?? '',
    generation: person ? String(person.generation) : '',
    clubEmail: person?.clubEmail ?? '',
    phone: person?.phone ?? '',
    personalEmail: person?.personalEmail ?? '',
    birthDate: person?.birthDate ? formatDate(person.birthDate) : '',
    facebookUrl: person?.facebookUrl ?? '',
    discordUserId: person?.discordUserId ?? '',
    isAdmin: person?.isAdmin ?? false,
  };
}

// Builds the API body; only fields that changed are sent so a non-admin edit never touches admin-only fields.
export function toBody(values: PersonFormValues, initial: PersonFormValues, includeKind: boolean): Record<string, unknown> {
  const body: Record<string, unknown> = {};
  const set = (key: keyof PersonFormValues, value: unknown) => {
    if (values[key] !== initial[key] || (includeKind && ['studentId', 'fullName', 'departmentId', 'generation'].includes(key))) body[key] = value;
  };
  if (includeKind) body.kind = values.kind;
  set('studentId', values.studentId);
  set('fullName', values.fullName);
  set('departmentId', values.departmentId);
  set('generation', Number(values.generation));
  set('phone', values.phone.trim() || null);
  set('personalEmail', values.personalEmail.trim() || null);
  // dd/MM/yyyy → yyyy-MM-dd; the server rejects impossible dates like 31/02.
  set('birthDate', values.birthDate.trim() ? values.birthDate.trim().split('/').reverse().join('-') : null);
  set('facebookUrl', values.facebookUrl.trim() || null);
  set('clubEmail', values.clubEmail.trim() || null);
  set('discordUserId', values.discordUserId.trim() || null);
  set('isAdmin', values.isAdmin);
  return body;
}

export function PersonForm({
  initial,
  showKind,
  submitLabel,
  onSubmit,
  disabled,
}: {
  initial: PersonFormValues;
  showKind: boolean;
  submitLabel: string;
  onSubmit: (values: PersonFormValues) => Promise<unknown>;
  disabled?: boolean;
}) {
  const me = useMe();
  const [values, setValues] = useState(initial);
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  const departments = useQuery({ queryKey: ['departments'], queryFn: () => api<Department[]>('/departments') });
  const update = <K extends keyof PersonFormValues>(key: K, value: PersonFormValues[K]) => setValues((v) => ({ ...v, [key]: value }));

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await onSubmit(values);
    } catch (e) {
      setError(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={(e) => void submit(e)} className="space-y-4">
      <fieldset disabled={disabled || saving} className="grid gap-3 sm:grid-cols-2">
        {showKind && (
          <Field label="Loại">
            <Select value={values.kind} onChange={(e) => update('kind', e.target.value as PersonKind)}>
              <option value="Candidate">Probation</option>
              <option value="Member">Thành viên</option>
            </Select>
          </Field>
        )}
        <Field label="Họ tên"><Input required value={values.fullName} onChange={(e) => update('fullName', e.target.value)} /></Field>
        <Field label="MSSV"><Input required value={values.studentId} onChange={(e) => update('studentId', e.target.value)} /></Field>
        <Field label="Ban">
          <Select required value={values.departmentId} onChange={(e) => update('departmentId', e.target.value)}>
            <option value="" disabled>Chọn ban</option>
            {departments.data?.filter((d) => d.active || d.id === values.departmentId).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </Field>
        <Field label="Gen"><Input required type="number" min={1} max={999} value={values.generation} onChange={(e) => update('generation', e.target.value)} /></Field>
        <Field label="Số điện thoại"><Input type="tel" value={values.phone} onChange={(e) => update('phone', e.target.value)} /></Field>
        <Field label="Email cá nhân"><Input type="email" value={values.personalEmail} onChange={(e) => update('personalEmail', e.target.value)} /></Field>
        <Field label="Ngày sinh"><Input placeholder="dd/MM/yyyy" pattern="\d{2}/\d{2}/\d{4}" title="dd/MM/yyyy, ví dụ 05/09/2004" inputMode="numeric" value={values.birthDate} onChange={(e) => update('birthDate', e.target.value)} /></Field>
        <Field label="Link Facebook"><Input type="url" placeholder="https://facebook.com/..." value={values.facebookUrl} onChange={(e) => update('facebookUrl', e.target.value)} /></Field>
        {me.isAdmin && (
          <>
            <Field label="Email CLB (chỉ admin)" hint="Có email thì người này đăng nhập web được."><Input type="email" value={values.clubEmail} onChange={(e) => update('clubEmail', e.target.value)} /></Field>
            <Field label="Discord user id (chỉ admin)" hint="Thường để trống; người dùng tự link bằng /verification."><Input inputMode="numeric" value={values.discordUserId} onChange={(e) => update('discordUserId', e.target.value)} /></Field>
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <input type="checkbox" checked={values.isAdmin} onChange={(e) => update('isAdmin', e.target.checked)} />
              Admin web (toàn quyền)
            </label>
          </>
        )}
      </fieldset>
      <ErrorText error={error} />
      {!disabled && <Button type="submit" disabled={saving}>{saving ? 'Đang lưu…' : submitLabel}</Button>}
    </form>
  );
}
