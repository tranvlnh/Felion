import { useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router';
import { api, type WriteResult } from '../api';
import { Card, PageHeader } from '../ui';
import { PersonForm, toBody, toFormValues } from './PersonForm';

export function PersonCreatePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [params] = useSearchParams();
  const initial = { ...toFormValues(), kind: params.get('kind') === 'Member' ? 'Member' as const : 'Candidate' as const };

  return (
    <>
      <PageHeader title="Thêm người" />
      <Card>
        <p className="mb-4 text-sm text-slate-500">Probation mới được gán role Probation, thành viên mới được gán role Member (chọn ở Cài đặt).</p>
        <PersonForm
          initial={initial}
          showKind
          submitLabel="Tạo"
          onSubmit={async (values) => {
            const result = await api<WriteResult>('/people', { method: 'POST', body: toBody(values, initial, true) });
            await Promise.all([
              queryClient.invalidateQueries({ queryKey: ['people'] }),
              queryClient.invalidateQueries({ queryKey: ['probation'] }),
            ]);
            navigate(`/people/${result.person.id}`);
          }}
        />
      </Card>
    </>
  );
}
