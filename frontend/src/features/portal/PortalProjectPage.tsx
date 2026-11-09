import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, MessageSquarePlus, SearchX } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { api } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { qk } from '@/lib/queryKeys'
import type { PagedResult } from '@/lib/types'
import type { PortalProjectDetail, PortalTimelineItem } from '@/lib/portalTypes'
import { EditorialSection, Kicker } from './kit/editorial'
import { designTypeLabel, portalProjectStatus } from './labels'
import { ProgressBar } from './ProgressBar'
import { DeliverableReview } from './DeliverableReview'
import { PortalDeliverableLinks } from './PortalDeliverableLinks'
import { deliveredText, remainingRoundsText } from './progress'
import { deliverableBadge } from './review'
import { PortalQuotationList } from './PortalQuotationList'
import { PortalRequestList } from './PortalRequestList'
import { acceptsRequests } from './requests'
import { usePortalQuotations, usePortalRequests, usePortalRevisions } from './usePortal'
import { PortalRevisionsView } from './PortalRevisionsView'
import { PortalImagesSection } from './PortalImagesSection'
import { PortalTimeline } from './PortalTimeline'

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
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    )
  }

  if (error || !project) {
    return (
      <div className="border-t-[3px] border-rule">
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy dự án"
          description="Dự án không tồn tại hoặc không thuộc tài khoản của bạn."
          action={
            <Button asChild variant="outline" className="h-11">
              <Link to="/portal">Về danh sách dự án</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const summary = project.deliverableSummary
  const canRequest = acceptsRequests(project.status)
  const dates = [
    project.startDate ? { label: 'Bắt đầu', value: formatDate(project.startDate) } : null,
    project.dueDate ? { label: 'Hạn hoàn thành', value: formatDate(project.dueDate) } : null,
  ].filter((x): x is { label: string; value: string } => !!x)

  return (
    <div className="space-y-6">
      <Link to="/portal" className="-ml-1 inline-flex min-h-11 items-center gap-1 px-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Tất cả dự án
      </Link>

      <header className="-mt-2">
        <Kicker tone="highlight">Dự án</Kicker>
        <h1 className="mt-2 break-words font-display text-[2rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">{project.name}</h1>
        <div className="mt-1.5 flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <p className="font-serif text-base italic text-muted-foreground">Loại thiết kế: {designTypeLabel[project.designType] ?? project.designType}</p>
          <StatusBadge status={project.status} label={portalProjectStatus[project.status]} />
        </div>

        {project.statusReason && (
          <p className="mt-3 whitespace-pre-wrap break-words border-l-4 border-warning py-1 pl-4 font-serif text-base italic text-foreground">
            {project.status === 'ON_HOLD' ? 'Tạm dừng: ' : ''}
            {project.statusReason}
          </p>
        )}

        {dates.length > 0 && (
          <dl className={`mt-4 grid border-y border-border ${dates.length > 1 ? 'grid-cols-2' : 'grid-cols-1'}`}>
            {dates.map((d, i) => (
              <div key={d.label} className={i > 0 ? 'border-l border-border py-3 pl-4' : 'py-3 pr-4'}>
                <dt className="text-xs text-muted-foreground">{d.label}</dt>
                <dd className="m-0 mt-0.5 font-display text-lg font-medium text-foreground">{d.value}</dd>
              </div>
            ))}
          </dl>
        )}

        <div className="mt-4 space-y-2">
          <p className="font-display text-[1.25rem] font-medium text-foreground">{deliveredText(summary)}</p>
          <ProgressBar summary={summary} />
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
            {summary.delivered > 0 && (
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 bg-foreground" aria-hidden="true" />
                {summary.delivered} đã bàn giao
              </span>
            )}
            {summary.approved > 0 && (
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 bg-success" aria-hidden="true" />
                {summary.approved} đã duyệt
              </span>
            )}
            {summary.awaitingClient > 0 && (
              <span className="inline-flex items-center gap-1.5 font-medium text-highlight-text">
                <span className="h-2 w-2 border-2 border-highlight" aria-hidden="true" />
                {summary.awaitingClient} chờ bạn duyệt
              </span>
            )}
          </div>
          <p className="text-xs text-muted-foreground">Sản phẩm được tính là xong khi nhà thiết kế đã xác nhận bạn duyệt hoặc đã bàn giao.</p>
        </div>
      </header>

      <EditorialSection id="portal-deliverables" number="I." title="Sản phẩm">
        {project.deliverables.length === 0 ? (
          <p className="mt-2 font-serif text-base italic text-muted-foreground">Chưa có sản phẩm nào.</p>
        ) : (
          <ul className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
            {project.deliverables.map(d => {
              const badge = deliverableBadge(d)
              return (
                <li key={d.id} className="py-4">
                  <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
                    <p className="min-w-0 break-words font-display text-[1.375rem] font-medium leading-snug text-foreground">
                      {d.name}
                      {d.quantity > 1 && <span className="font-sans text-sm font-normal text-muted-foreground"> · Số lượng {d.quantity}</span>}
                    </p>
                    <StatusBadge status={badge.status} label={badge.label} />
                  </div>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {remainingRoundsText(d)}
                    {d.dueDate && ` · Hạn ${formatDate(d.dueDate)}`}
                  </p>
                  <DeliverableReview projectId={project.id} projectStatus={project.status} deliverable={d} />
                  <PortalDeliverableLinks links={d.links ?? []} />
                </li>
              )
            })}
          </ul>
        )}
        {project.deliverables.some(d => d.includedRounds > 0) && (
          <p className="mt-3 max-w-[60ch] text-xs leading-relaxed text-muted-foreground">
            Mỗi lần bạn góp ý cho một sản phẩm dùng 1 lượt chỉnh sửa miễn phí (dù nhiều ý). Lỗi do nhà thiết kế không tính.
            Hết lượt miễn phí, nhà thiết kế sẽ báo giá trước khi sửa tiếp.
          </p>
        )}
      </EditorialSection>

      <PortalImagesSection projectId={project.id} number="II." />

      <EditorialSection id="portal-quotations" number="III." title="Báo giá">
        {!quotations ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : quotations.length === 0 ? (
          <p className="mt-2 font-serif text-base italic text-muted-foreground">Chưa có báo giá nào cho dự án này.</p>
        ) : (
          <PortalQuotationList items={quotations} />
        )}
      </EditorialSection>

      <EditorialSection
        id="portal-requests"
        number="IV."
        title="Yêu cầu chỉnh sửa của bạn"
        aside={<Link to="/portal/requests" className="link-inline inline-flex min-h-11 items-center whitespace-nowrap">Xem tất cả</Link>}
      >
        {canRequest && (
          <Button asChild variant="outline" className="mb-2 mt-1 h-11 w-full sm:w-auto">
            <Link to={`/portal/requests/new?projectId=${project.id}`}>
              <MessageSquarePlus aria-hidden="true" />
              Gửi yêu cầu mới
            </Link>
          </Button>
        )}
        {!requests ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : requests.length === 0 ? (
          <p className="mt-2 font-serif text-base italic text-muted-foreground">Bạn chưa gửi yêu cầu nào cho dự án này.</p>
        ) : (
          <PortalRequestList items={requests} />
        )}
        {!canRequest && <p className="mt-3 text-xs text-muted-foreground">Dự án đã kết thúc nên không nhận yêu cầu mới.</p>}
      </EditorialSection>

      <EditorialSection id="portal-revisions" number="V." title="Lượt chỉnh sửa">
        {!revisions ? <Skeleton className="mt-3 h-16 w-full" /> : <PortalRevisionsView data={revisions} />}
      </EditorialSection>

      <EditorialSection id="portal-brief" number="VI." title="Yêu cầu thiết kế đã thống nhất">
        {project.brief ? (
          <details className="mt-1" open>
            <summary className="flex min-h-11 cursor-pointer items-center font-serif text-sm italic text-muted-foreground">
              Lần chốt {project.brief.version}{project.brief.confirmedAt ? ` · ${formatDate(project.brief.confirmedAt)}` : ''}
            </summary>
            <div className="mt-1 max-w-[60ch] whitespace-pre-wrap break-words text-sm leading-relaxed text-foreground">
              {project.brief.content || 'Chưa có nội dung'}
            </div>
            {canRequest && <p className="mt-3 text-xs text-muted-foreground">Muốn đổi yêu cầu? Bấm “Gửi yêu cầu mới”.</p>}
          </details>
        ) : (
          <p className="mt-2 font-serif text-base italic text-muted-foreground">Nhà thiết kế chưa chốt yêu cầu thiết kế.</p>
        )}
      </EditorialSection>

      <EditorialSection id="portal-timeline" number="VII." title="Hoạt động gần đây">
        {!timeline ? (
          <Skeleton className="mt-3 h-16 w-full" />
        ) : timeline.items.length === 0 ? (
          <p className="mt-2 font-serif text-base italic text-muted-foreground">Chưa có cập nhật nào.</p>
        ) : (
          <PortalTimeline items={timeline.items} />
        )}
      </EditorialSection>
    </div>
  )
}
