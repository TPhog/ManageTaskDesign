import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight, FolderOpen, PencilRuler, Sparkles } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { formatDate } from '@/lib/format'
import { EditorialSection, StatusText } from './kit/editorial'
import { designTypeLabel, portalProjectStatus } from './labels'
import { ProgressBar } from './ProgressBar'
import { PortalTodoList } from './PortalTodoList'
import { PortalWelcome } from './PortalWelcome'
import { deliveredText, waitingText } from './progress'
import { buildTodoList } from './todo'
import { usePortalMe, usePortalProjects, usePortalQuotations } from './usePortal'

/** CF-14: a letter to the client — greeting, "Việc bạn cần làm", then the projects. */
export default function PortalHomePage() {
  const { data: me } = usePortalMe()
  const { data, isLoading, isError: projectsError, refetch: refetchProjects } = usePortalProjects()
  const { data: quotations, isLoading: quotationsLoading, isError: quotationsError, refetch: refetchQuotations } = usePortalQuotations()
  // A failed load must never read as "nothing to do" / "no projects" (review I-3)
  const retry = (
    <Button type="button" variant="outline" className="h-11" onClick={() => { void refetchProjects(); void refetchQuotations() }}>
      Thử lại
    </Button>
  )
  // P13: the designer's public form, for a brand-new design order (hidden while the form is off)
  const intakeUrl = me?.designer.intakeUrl ?? null
  const orderNew = intakeUrl && (
    <Button asChild variant="outline" size="touch" className="w-full sm:w-auto">
      <a href={intakeUrl}>
        <PencilRuler aria-hidden="true" />
        Đặt thiết kế mới
      </a>
    </Button>
  )
  // QA C-06 / BR-48: projects that wait for the client come first; the rest keep the server's order
  const projects = data
    ? [...data].sort((a, b) => Number(b.deliverableSummary.awaitingClient > 0) - Number(a.deliverableSummary.awaitingClient > 0))
    : undefined

  return (
    <div className="space-y-8">
      {me && <PortalWelcome clientName={me.clientName} studioName={me.designer.displayName || 'Nhà thiết kế'} />}

      {isLoading || quotationsLoading ? (
        <Skeleton className="h-24 w-full" />
      ) : projectsError || quotationsError ? (
        <EditorialSection id="portal-todo" number="I." title="Việc bạn cần làm">
          <LoadError what="việc cần làm" action={retry} />
        </EditorialSection>
      ) : (
        <PortalTodoList number="I." items={buildTodoList(projects ?? [], quotations ?? [])} />
      )}

      <EditorialSection id="portal-projects" number="II." title="Dự án của bạn">
        {isLoading ? (
          <div className="mt-3 space-y-3">
            {Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-24 w-full" />)}
          </div>
        ) : projectsError ? (
          <LoadError what="danh sách dự án" action={retry} />
        ) : !projects || projects.length === 0 ? (
          <EmptyState
            icon={FolderOpen}
            title="Chưa có dự án nào"
            description="Khi nhà thiết kế tạo dự án cho bạn, dự án sẽ hiện ở đây."
            action={orderNew || undefined}
          />
        ) : (
          <ul className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
            {projects.map(p => {
              const waiting = waitingText(p.deliverableSummary)
              return (
                <li key={p.id} className="py-4">
                  <Link
                    to={`/portal/projects/${p.id}`}
                    className="group block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        {/* QA C-08: long names wrap to two lines instead of being cut */}
                        <p className="line-clamp-2 font-display text-[1.25rem] font-medium leading-snug text-foreground group-hover:underline group-hover:decoration-highlight group-hover:underline-offset-4">
                          {p.name}
                        </p>
                        <p className="mt-0.5 font-serif text-sm italic text-muted-foreground">
                          {designTypeLabel[p.designType] ?? p.designType}
                          {p.dueDate && ` · hạn ${formatDate(p.dueDate)}`}
                        </p>
                      </div>
                      <ChevronRight className="mt-1.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                    </div>
                    <div className="mt-3 space-y-1.5">
                      <ProgressBar summary={p.deliverableSummary} />
                      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-xs text-muted-foreground">
                        <span>{deliveredText(p.deliverableSummary)}</span>
                        <StatusBadge status={p.status} label={portalProjectStatus[p.status]} />
                      </div>
                    </div>
                    {waiting && <div className="mt-2"><StatusText tone="highlight">{waiting}</StatusText></div>}
                  </Link>
                  {/* A sibling of the project link (links cannot nest) */}
                  {p.hasOpenQuotation && (
                    <Link
                      to={p.openQuotationId ? `/portal/quotations/${p.openQuotationId}` : '/portal/quotations'}
                      aria-label={`Xem báo giá mới của ${p.name}`}
                      className="link-inline mt-1 inline-flex min-h-11 items-center gap-1.5 text-sm"
                    >
                      <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
                      Có báo giá mới
                    </Link>
                  )}
                </li>
              )
            })}
          </ul>
        )}
        {orderNew && projects && projects.length > 0 && <div className="mt-4">{orderNew}</div>}
      </EditorialSection>
    </div>
  )
}

function LoadError({ what, action }: { what: string; action: ReactNode }) {
  return (
    <div role="alert" className="space-y-3 border-b border-border py-3">
      <p className="font-serif text-base italic text-foreground">Không tải được {what}. Vui lòng kiểm tra kết nối rồi thử lại.</p>
      {action}
    </div>
  )
}
