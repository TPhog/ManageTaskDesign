import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ImageIcon, UserPlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { Pagination } from '@/components/Pagination'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDatetime } from '@/lib/format'
import type { IntakeStatus } from '@/lib/requestTemplateTypes'
import { IntakeSheet } from './IntakeSheet'
import { intakeStatusBadge, intakeStatusLabel } from './intakeUtils'
import { INTAKE_PAGE_SIZE, useIntakeList } from './useIntake'

const tabs: IntakeStatus[] = ['NEW', 'CONVERTED', 'REJECTED']

/** Plan 13 /admin/intake: submissions of the public form; /admin/intake/:id opens the sheet. */
export default function IntakePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [status, setStatus] = useState<IntakeStatus>('NEW')
  const [page, setPage] = useState(1)
  const { data, isLoading, error, refetch } = useIntakeList(status, page)

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Khách tự gửi qua form công khai"
        title="Khách mới"
        description="Yêu cầu từ form công khai — chuyển thành khách & dự án chỉ với một lần bấm."
      />

      <Tabs value={status} onValueChange={v => { setStatus(v as IntakeStatus); setPage(1) }}>
        <TabsList>
          {tabs.map(t => <TabsTrigger key={t} value={t}>{intakeStatusLabel[t]}</TabsTrigger>)}
        </TabsList>
      </Tabs>

      <div className="rounded-xl border bg-card shadow-card">
        {isLoading ? (
          <div className="space-y-3 p-4">
            {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-16 w-full" />)}
          </div>
        ) : error ? (
          <EmptyState
            icon={UserPlus}
            title="Không tải được danh sách"
            action={<Button variant="outline" onClick={() => refetch()}>Thử lại</Button>}
          />
        ) : !data || data.items.length === 0 ? (
          status === 'NEW' ? (
            <EmptyState
              icon={UserPlus}
              title="Chưa có khách mới"
              description="Bật form khách mới trong Cài đặt rồi chia sẻ link lên Facebook/Zalo."
              action={<Button asChild variant="outline"><Link to="/admin/settings?tab=portal">Mở Cài đặt</Link></Button>}
            />
          ) : (
            <EmptyState icon={UserPlus} title="Chưa có hồ sơ nào" />
          )
        ) : (
          <ul className="divide-y">
            {data.items.map(s => (
              <li key={s.id}>
                <Link
                  to={`/admin/intake/${s.id}`}
                  className="flex flex-col gap-1.5 px-4 py-3.5 transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:flex-row sm:items-center sm:gap-4"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-foreground">{s.contactName} · {s.templateName ?? 'Yêu cầu khác'}</p>
                    <p className="line-clamp-2 text-sm text-muted-foreground">{s.excerpt}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3 text-xs text-muted-foreground">
                    {s.imageCount > 0 && (
                      <span className="inline-flex items-center gap-1" aria-label={`${s.imageCount} ảnh`}>
                        <ImageIcon className="h-3.5 w-3.5" aria-hidden="true" />{s.imageCount}
                      </span>
                    )}
                    <span className="tabular-nums">{formatDatetime(s.createdAt)}</span>
                    <StatusBadge status={intakeStatusBadge[s.status]} label={intakeStatusLabel[s.status]} />
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
        <Pagination page={page} pageSize={INTAKE_PAGE_SIZE} total={data?.total ?? 0} onPageChange={setPage} itemLabel="hồ sơ" />
      </div>

      <IntakeSheet id={id} onClose={() => navigate('/admin/intake')} />
    </div>
  )
}
