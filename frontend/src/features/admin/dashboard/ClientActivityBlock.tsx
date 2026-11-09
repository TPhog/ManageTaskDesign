import { Link } from 'react-router-dom'
import { CheckCircle2, FileCheck2, Inbox, MessageSquare, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { ClientActivity, ClientActivityKind } from '@/lib/dashboardTypes'
import { formatDatetime } from '@/lib/format'
import { Section } from './DashboardSection'
import { useClientActivity } from './useDashboardBlocks'

const KIND_ICON = {
  REQUEST_CREATED: Inbox,
  DELIVERABLE_APPROVED: CheckCircle2,
  QUOTATION_ACCEPTED: FileCheck2,
} satisfies Record<ClientActivityKind, typeof Inbox>

/** Presentational: what clients did lately; every row links to the server-built `href`. */
export function ClientActivityView({ activity }: { activity: ClientActivity }) {
  if (activity.items.length === 0) {
    return <p className="text-sm text-muted-foreground">Chưa có hoạt động nào từ khách.</p>
  }
  return (
    <ul>
      {activity.items.map(i => {
        const Icon = KIND_ICON[i.kind] ?? Inbox
        const who = [i.clientName, i.projectName].filter(Boolean).join(' · ')
        return (
          <li key={i.id} className="border-t border-border first:border-t-0">
            <Link
              to={i.href}
              className="group flex min-h-11 items-start gap-3 rounded-md py-2.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-surface-hover text-foreground">
                <Icon className="h-3.5 w-3.5" aria-hidden="true" />
              </span>
              <span className="min-w-0">
                <span className="block break-words text-sm font-medium text-foreground group-hover:underline group-hover:decoration-highlight group-hover:decoration-[1.5px] group-hover:underline-offset-[3px]">{i.text}</span>
                <span className="block truncate text-xs text-muted-foreground">
                  {who && `${who} · `}{formatDatetime(i.occurredAt)}
                </span>
              </span>
            </Link>
          </li>
        )
      })}
    </ul>
  )
}

export function ClientActivityBlock({ className }: { className?: string }) {
  const { data, isLoading, error, refetch } = useClientActivity(10)
  return (
    <Section title="Hoạt động của khách" icon={MessageSquare} className={className}>
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-11 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được hoạt động của khách.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <ClientActivityView activity={data} />
      )}
    </Section>
  )
}
