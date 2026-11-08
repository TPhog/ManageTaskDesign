import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays, Clock, FolderOpen, Sparkles } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { api } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { qk } from '@/lib/queryKeys'
import { cn } from '@/lib/utils'
import type { PortalProjectListItem } from '@/lib/portalTypes'
import { designTypeLabel, portalProjectStatus } from './labels'
import { ProgressBar } from './ProgressBar'
import { deliveredText, waitingText } from './progress'

export default function PortalHomePage() {
  const { data, isLoading } = useQuery({
    queryKey: qk.portal.projects,
    queryFn: () => api.get<PortalProjectListItem[]>('/portal/projects'),
  })
  // QA C-06: projects that wait for the client come first; the rest keep the server's order
  const projects = data
    ? [...data].sort((a, b) => Number(b.deliverableSummary.inReview > 0) - Number(a.deliverableSummary.inReview > 0))
    : undefined

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-foreground">Dự án của bạn</h1>
        <p className="mt-1 text-sm text-slate-600">Theo dõi tiến độ và các hạng mục nhà thiết kế đang thực hiện.</p>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      ) : !projects || projects.length === 0 ? (
        <div className="rounded-xl border bg-card shadow-card">
          <EmptyState
            icon={FolderOpen}
            title="Chưa có dự án nào"
            description="Khi nhà thiết kế tạo dự án cho bạn, dự án sẽ hiện ở đây."
          />
        </div>
      ) : (
        <ul className="space-y-3">
          {projects.map(p => {
            const waiting = waitingText(p.deliverableSummary)
            return (
            <li key={p.id} className="relative">
              <Link
                to={`/portal/projects/${p.id}`}
                className={cn(
                  'block rounded-xl border bg-card p-4 shadow-card transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  p.hasOpenQuotation && 'pb-14',
                )}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    {/* QA C-08: long names wrap to two lines instead of being cut */}
                    <p className="line-clamp-2 font-semibold text-foreground">{p.name}</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">{designTypeLabel[p.designType] ?? p.designType}</p>
                  </div>
                  <StatusBadge status={p.status} label={portalProjectStatus[p.status]} />
                </div>
                <div className="mt-4 space-y-1.5">
                  <ProgressBar summary={p.deliverableSummary} />
                  <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
                    <span>{deliveredText(p.deliverableSummary)}</span>
                    {p.dueDate && (
                      <span className="inline-flex items-center gap-1 whitespace-nowrap">
                        <CalendarDays className="h-3.5 w-3.5" aria-hidden="true" />
                        Hạn {formatDate(p.dueDate)}
                      </span>
                    )}
                  </div>
                </div>
                {waiting && (
                  <p className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800 ring-1 ring-inset ring-amber-200">
                    <Clock className="h-3.5 w-3.5" aria-hidden="true" />
                    {waiting}
                  </p>
                )}
              </Link>
              {/* A sibling of the card link (links cannot nest), placed inside the card's bottom padding */}
              {p.hasOpenQuotation && (
                <Link
                  to={p.openQuotationId ? `/portal/quotations/${p.openQuotationId}` : '/portal/quotations'}
                  aria-label={`Xem báo giá mới của ${p.name}`}
                  className="absolute bottom-4 left-4 inline-flex min-h-8 items-center gap-1 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-200 transition-colors hover:bg-blue-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <Sparkles className="h-3 w-3" aria-hidden="true" />
                  Có báo giá mới
                </Link>
              )}
            </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
