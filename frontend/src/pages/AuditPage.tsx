import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { api, type AuditItem, type Page } from '../api';
import { Card, ErrorText, Loading, PageHeader, Pager } from '../ui';

const pageSize = 50;

export function AuditPage() {
  const [page, setPage] = useState(1);
  const audit = useQuery({
    queryKey: ['audit', page],
    queryFn: () => api<Page<AuditItem>>(`/audit?page=${page}&pageSize=${pageSize}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="Lịch sử thao tác" />
      <Card>
        <ErrorText error={audit.error} />
        {audit.isLoading ? <Loading /> : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800">
                <tr>
                  <th className="py-2 pr-3">Thời gian</th>
                  <th className="py-2 pr-3">Người làm</th>
                  <th className="py-2 pr-3">Hành động</th>
                  <th className="py-2 pr-3">Đối tượng</th>
                  <th className="py-2 pr-3">Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 align-top dark:divide-slate-800">
                {audit.data?.items.map((item) => (
                  <tr key={item.id}>
                    <td className="whitespace-nowrap py-2 pr-3 text-xs">{new Date(item.createdAt).toLocaleString('vi-VN')}</td>
                    <td className="py-2 pr-3">{item.actor ? <Link className="hover:underline" to={`?person=${item.actor.id}`}>{item.actor.fullName}</Link> : <span className="text-slate-500">hệ thống</span>}</td>
                    <td className="py-2 pr-3 font-mono text-xs">{item.action}</td>
                    <td className="py-2 pr-3 text-xs">
                      {item.entityType === 'Person' && item.entityId ? <Link className="hover:underline" to={`?person=${item.entityId}`}>Person</Link> : item.entityType}
                    </td>
                    <td className="max-w-md truncate py-2 pr-3 font-mono text-xs text-slate-500" title={JSON.stringify(item.metadata)}>
                      {item.metadata ? JSON.stringify(item.metadata) : ''}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {audit.data && <Pager page={page} pageSize={pageSize} total={audit.data.total} onPage={setPage} />}
      </Card>
    </>
  );
}
