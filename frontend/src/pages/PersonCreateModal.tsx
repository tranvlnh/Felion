import { useQueryClient } from '@tanstack/react-query';
import { api, type PersonKind, type WriteResult } from '../api';
import { Card, Modal } from '../ui';
import { PersonForm, toBody, toFormValues } from './PersonForm';

export function PersonCreateModal({ kind, onClose, onCreated }: { kind: PersonKind; onClose: () => void; onCreated: (id: string) => void }) {
  const queryClient = useQueryClient();
  const initial = { ...toFormValues(), kind };

  return (
    <Modal title="Thêm người" onClose={onClose}>
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
            onCreated(result.person.id);
          }}
        />
      </Card>
    </Modal>
  );
}
