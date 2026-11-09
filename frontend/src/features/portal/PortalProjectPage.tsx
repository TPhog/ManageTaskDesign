import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, MessageSquarePlus, SearchX } from 'lucide-react'
import { Button, buttonVariants } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { LinkifiedText } from '@/components/LinkifiedText'
import { MarkdownView } from '@/components/MarkdownView'
import { api } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { qk } from '@/lib/queryKeys'
import type { PagedResult } from '@/lib/types'
import type { PortalProjectDetail, PortalTimelineItem } from '@/lib/portalTypes'
import { designTypeLabel, portalDeliverableStatus, portalProjectStatus } from './labels'
import { ProgressBar } from './ProgressBar'
import { DeliverableReview } from './DeliverableReview'
import { deliveredText, progressOf, remainingRoundsText } from './progress'
import { groupTimeline, timeOfDay } from './timeline'
import { cn } from '@/lib/utils'
import { PortalQuotationList } from './PortalQuotationList'
import { PortalRequestList } from './PortalRequestList'
import { acceptsRequests } from './requests'
import { usePortalQuotations, usePortalRequests, usePortalRevisions } from './usePortal'
import { PortalRevisionsView } from './PortalRevisionsView'
import { PortalImageStrip, PortalImagesSection } from './PortalImagesSection'

const panelClass = 'rounded-xl border bg-card p-4 shadow-card sm:p-5'

export default function PortalProjectPage() {
  const { id = '' } = useParams<{ id: string }>()

  const { data: project, isLoading, error } = useQuery({
    queryKey: qk.portal.project(id),
    queryFn: () => api.get<PortalProjectDetail>(`/portal/projects/${id}`),
    enabled: !!id,
    retry: false,
  })

  const { data: timeline } = useQuery({
    queryKey: qk.portal.timeline(id),
    queryFn: () => api.get<PagedResult<PortalTimelineItem>>(`/portal/projects/${id}/timeline?pageSize=50`),
    enabled: !!project,
  })

  const { data: quotations } = usePortalQuotations(id, !!project)
  const { data: requests } = usePortalRequests(id, !!project)
  const { data: revisions } = usePortalRevisions(id, !!project)

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-40 w-full rounded-xl" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    )
  }

  if (error || !project) {
    return (
      <div className="rounded-xl border bg-card shadow-card">
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy dự án"
          description="Dự án không tồn tại hoặc không thuộc tài khoản của bạn."
          action={
            <Button asChild variant="outline">
              <Link to="/portal">Về danh sách dự án</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const { pct } = progressOf(project.deliverableSummary)

  const canRequest = acceptsRequests(project.status)

  return (
    <div className={cn('space-y-4', canRequest && 'pb-20')}>
      <Link to="/portal" className="-ml-1 inline-flex min-h-11 items-center gap-1 px-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Tất cả dự án
      </Link>

      <div className={panelClass}>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold tracking-tight text-foreground">{project.name}</h1>
            <p className="mt-0.5 text-sm text-muted-foreground">{designTypeLabel[project.designType] ?? project.designType}</p>
          </div>
          <StatusBadge status={project.status} label={portalProjectStatus[project.status]} />
        </div>

        {project.statusReason && (
          <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {project.status === 'ON_HOLD' ? 'Tạm dừng: ' : ''}
            {project.statusReason}
          </p>
        )}

        <dl className="mt-4 grid grid-cols-2 gap-3">
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Bắt đầu</dt>
            <dd className="mt-0.5 text-sm font-medium text-foreground">{formatDate(project.startDate)}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Hạn hoàn thành</dt>
            <dd className="mt-0.5 text-sm font-medium text-foreground">{formatDate(project.dueDate)}</dd>
          </div>
        </dl>

        <div className="mt-4 space-y-1.5">
          <div className="flex items-center justify-between gap-2 text-xs">
            <span className="font-medium text-foreground">Tiến độ {pct}%</span>
            <span className="text-muted-foreground">{deliveredText(project.deliverableSummary)}</span>
          </div>
          <ProgressBar summary={project.deliverableSummary} />
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
            {project.deliverableSummary.delivered > 0 && (
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-emerald-600" aria-hidden="true" />
                {project.deliverableSummary.delivered} đã bàn giao
              </span>
            )}
            {project.deliverableSummary.approved > 0 && (
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-sky-400" aria-hidden="true" />
                {project.deliverableSummary.approved} bạn đã duyệt
              </span>
            )}
            {project.deliverableSummary.inReview > 0 && (
              <span className="inline-flex items-center gap-1.5 font-medium text-amber-800">
                <span className="h-2 w-2 rounded-full border-2 border-amber-500" aria-hidden="true" />
                {project.deliverableSummary.inReview} chờ bạn duyệt
              </span>
            )}
          </div>
        </div>
      </div>

      <section aria-labelledby="portal-deliverables" className={panelClass}>
        <h2 id="portal-deliverables" className="text-sm font-semibold text-foreground">Hạng mục</h2>
        {project.deliverables.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">Chưa có hạng mục nào.</p>
        ) : (
          <ul className="mt-3 divide-y">
            {project.deliverables.map(d => (
              <li key={d.id} className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0">
                <div className="min-w-0">
                  <p className="font-medium text-foreground">
                    {d.name}
                    {d.quantity > 1 && <span className="font-normal text-muted-foreground"> · Số lượng {d.quantity}</span>}
                  </p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {remainingRoundsText(d)}
                    {d.dueDate && ` · Hạn ${formatDate(d.dueDate)}`}
                  </p>
                  <DeliverableReview projectId={project.id} projectStatus={project.status} deliverable={d} />
                </div>
                <StatusBadge status={d.status} label={portalDeliverableStatus[d.status]} />
              </li>
            ))}
          </ul>
        )}
        {project.deliverables.some(d => d.includedRounds > 0) && (
          <p className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">
            Mỗi lần bạn gửi góp ý cho một hạng mục được tính là 1 lần sửa (dù có nhiều ý). Lỗi do nhà thiết kế thì không tính.
            Hết lần sửa miễn phí, nhà thiết kế sẽ báo giá trước khi sửa tiếp.
          </p>
        )}
      </section>

      <PortalImagesSection projectId={project.id} />

      <section aria-labelledby="portal-quotations" className={panelClass}>
        <h2 id="portal-quotations" className="text-sm font-semibold text-foreground">Báo giá</h2>
        {!quotations ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : quotations.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">Chưa có báo giá nào cho dự án này.</p>
        ) : (
          <div className="mt-3">
            <PortalQuotationList items={quotations} />
          </div>
        )}
      </section>

      <section aria-labelledby="portal-requests" className={panelClass}>
        <div className="flex items-center justify-between gap-3">
          <h2 id="portal-requests" className="text-sm font-semibold text-foreground">Yêu cầu chỉnh sửa của bạn</h2>
          <Link to="/portal/requests" className="text-xs font-medium text-primary hover:underline">Xem tất cả</Link>
        </div>
        {!requests ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : requests.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">Bạn chưa gửi yêu cầu nào cho dự án này.</p>
        ) : (
          <div className="mt-3">
            <PortalRequestList items={requests} />
          </div>
        )}
        {!canRequest && (
          <p className="mt-3 text-xs text-muted-foreground">Dự án đã kết thúc nên không nhận yêu cầu mới.</p>
        )}
      </section>

      <section aria-labelledby="portal-revisions" className={panelClass}>
        <h2 id="portal-revisions" className="text-sm font-semibold text-foreground">Lần sửa</h2>
        {!revisions ? <Skeleton className="mt-3 h-16 w-full" /> : <PortalRevisionsView data={revisions} />}
      </section>

      <section aria-labelledby="portal-brief" className={panelClass}>
        <h2 id="portal-brief" className="text-sm font-semibold text-foreground">Yêu cầu thiết kế (brief)</h2>
        {project.brief ? (
          <details className="mt-1" open>
            <summary className="flex min-h-11 cursor-pointer items-center text-sm font-medium text-primary">
              Nội dung đã thống nhất · phiên bản {project.brief.version} · xác nhận {formatDate(project.brief.confirmedAt)}
            </summary>
            <div className="mt-2">
              <MarkdownView source={project.brief.content || 'Chưa có nội dung'} />
            </div>
            {canRequest && (
              <p className="mt-3 text-xs text-slate-600">Muốn đổi yêu cầu thiết kế? Bấm “Gửi yêu cầu chỉnh sửa”.</p>
            )}
          </details>
        ) : (
          <p className="mt-2 text-sm text-muted-foreground">Nhà thiết kế chưa chốt yêu cầu thiết kế.</p>
        )}
      </section>

      <section aria-labelledby="portal-timeline" className={panelClass}>
        <h2 id="portal-timeline" className="text-sm font-semibold text-foreground">Tiến trình</h2>
        {!timeline ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : timeline.items.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">Chưa có cập nhật nào.</p>
        ) : (
          <div className="mt-4 space-y-5">
            {groupTimeline(timeline.items).map(day => (
              <div key={day.day}>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-600">{day.label}</h3>
                <ol className="mt-2 space-y-3 border-l border-slate-200 pl-5">
                  {day.items.map((t, i) => (
                    <li key={`${t.occurredAt}-${i}`} className="relative">
                      <span
                        className={cn(
                          'absolute -left-[25.5px] top-1.5 h-2.5 w-2.5 rounded-full ring-4 ring-white',
                          t.kind === 'MESSAGE' ? 'bg-amber-500' : 'bg-primary',
                        )}
                        aria-hidden="true"
                      />
                      {t.kind === 'MESSAGE' ? (
                        <div className="rounded-lg bg-amber-50 px-3 py-2">
                          <p className="text-xs font-medium text-amber-800">Nhà thiết kế nhắn</p>
                          <LinkifiedText text={t.message} className="mt-0.5 block text-sm text-foreground" />
                        </div>
                      ) : (
                        <LinkifiedText text={t.message} className="block text-sm text-foreground" />
                      )}
                      <PortalImageStrip images={t.images ?? []} />
                      <time dateTime={t.occurredAt} className="text-xs text-muted-foreground">{timeOfDay(t.occurredAt)}</time>
                    </li>
                  ))}
                </ol>
              </div>
            ))}
          </div>
        )}
      </section>

      {canRequest && (
        <Link
          to={`/portal/requests/new?projectId=${project.id}`}
          className={cn(buttonVariants(), 'fixed bottom-[calc(5rem+env(safe-area-inset-bottom))] right-4 z-20 shadow-lg sm:right-[max(1rem,calc(50%-20rem))]')}
        >
          <MessageSquarePlus aria-hidden="true" />
          Gửi yêu cầu chỉnh sửa
        </Link>
      )}
    </div>
  )
}
