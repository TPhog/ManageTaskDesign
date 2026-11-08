import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Inbox, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDatetime } from '@/lib/format'
import { requestSourceLabel, requestStatusLabel } from '@/lib/revisionLabels'
import type { RequestStatus } from '@/lib/revisionTypes'
import { useRequests } from '@/features/admin/revisions/useRevisions'
import { CreateRequestDialog } from './CreateRequestDialog'
import { RequestSheet } from './RequestSheet'

const tabs: RequestStatus[] = ['NEW', 'TRIAGED', 'DECLINED']

const emptyText: Record<RequestStatus, string> = {
  NEW: 'Yêu cầu khách gửi qua Portal hoặc bạn ghi nhận hộ sẽ hiện ở đây.',
  TRIAGED: 'Chưa có yêu cầu nào được tiếp nhận.',
  DECLINED: 'Chưa có yêu cầu nào bị từ chối.',
}

/** docs/08 §2.2 /admin/requests (REQ-03): tabs NEW / Đã xử lý / Từ chối; /admin/requests/:id opens the sheet. */
export default function RequestsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [status, setStatus] = useState<RequestStatus>('NEW')
  const [createOpen, setCreateOpen] = useState(false)
  const { data, isLoading, error, refetch } = useRequests(status)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Yêu cầu"
        description="Yêu cầu chỉnh sửa của khách — tiếp nhận thành vòng sửa hoặc từ chối."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus aria-hidden="true" />
            Ghi nhận yêu cầu
          </Button>
        }
      />

      <Tabs value={status} onValueChange={v => setStatus(v as RequestStatus)}>
        <TabsList>
          {tabs.map(t => <TabsTrigger key={t} value={t}>{requestStatusLabel[t]}</TabsTrigger>)}
        </TabsList>
      </Tabs>

      <div className="rounded-xl border bg-card shadow-card">
        {isLoading ? (
          <div className="space-y-3 p-4">
            {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
          </div>
        ) : error ? (
          <div className="flex items-center gap-2 p-6 text-sm text-muted-foreground">
            Không tải được danh sách yêu cầu.
            <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
          </div>
        ) : !data || data.items.length === 0 ? (
          <EmptyState icon={Inbox} title="Không có yêu cầu" description={emptyText[status]} />
        ) : (
          <ul className="divide-y">
            {data.items.map(r => (
              <li key={r.id}>
                <Link
                  to={`/admin/requests/${r.id}`}
                  className="flex flex-col gap-1.5 px-4 py-3.5 transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:flex-row sm:items-center sm:gap-4"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-foreground">
                      {r.clientName} · {r.projectName}{r.deliverableName ? ` · ${r.deliverableName}` : ''}
                    </p>
                    <p className="line-clamp-2 text-sm text-muted-foreground">{r.content}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3 text-xs text-muted-foreground">
                    <span>{requestSourceLabel[r.source]}</span>
                    <span className="tabular-nums">{formatDatetime(r.createdAt)}</span>
                    <StatusBadge status={r.status} label={requestStatusLabel[r.status]} />
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      <RequestSheet requestId={id} onClose={() => navigate('/admin/requests')} />
      <CreateRequestDialog open={createOpen} onClose={() => setCreateOpen(false)} onCreated={rid => navigate(`/admin/requests/${rid}`)} />
    </div>
  )
}
