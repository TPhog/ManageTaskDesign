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
    <ul className="divide-y">
      {activity.items.map(i => {
        const Icon = KIND_ICON[i.kind] ?? Inbox
        const who = [i.clientName, i.projectName].filter(Boolean).join(' · ')
        return (
          <li key={i.id}>
            <Link
              to={i.href}
              className="flex min-h-11 items-start gap-3 rounded-md py-2.5 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Icon className="mt-0.5 h-4 w-4 shrink-0 text-primary" aria-hidden="true" />
              <span className="min-w-0">
                <span className="block break-words text-sm text-foreground">{i.text}</span>
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

export function ClientActivityBlock() {
  const { data, isLoading, error, refetch } = useClientActivity(10)
  return (
    <Section title="Hoạt động của khách" icon={MessageSquare}>
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-11 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được hoạt động của khách.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <ClientActivityView activity={data} />
      )}
    </Section>
  )
}
