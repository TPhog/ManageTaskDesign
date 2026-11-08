import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Inbox, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { Dashboard } from '@/lib/dashboardTypes'
import { cn } from '@/lib/utils'
import { FlowActionButton, FlowWarningChip } from '@/features/admin/flow/FlowActionControls'
import { flowActionKey, stageLabel } from '@/features/admin/flow/flowUtils'
import { useFlowActions } from '@/features/admin/flow/useFlow'
import { useFlowActionRunner } from '@/features/admin/flow/useFlowActionRunner'
import { DASHBOARD_FLOW_LIMIT, dashboardLinks, projectsNeedingAction } from './dashboardUtils'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

/** One DSH-01 counter; the whole chip links to the filtered list (docs/08 §2.2). `alert` = needs attention (text, not color alone). */
function Counter({ to, label, value, testId, alert = false }: {
  to: string
  label: string
  value: number
  testId: string
  alert?: boolean
}) {
  return (
    <li>
      <Link
        to={to}
        className="inline-flex items-center gap-2 rounded-full border px-3 py-1 text-sm text-muted-foreground transition-colors duration-150 hover:border-primary/40 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {label}
        <span data-testid={testId} className={cn('font-semibold tabular-nums', alert ? 'text-red-700' : 'text-foreground')}>
          {value}
        </span>
      </Link>
    </li>
  )
}

/**
 * Spec 06b §6 dashboard "Việc cần làm": next steps of every open project from GET /api/admin/flow/actions, run with the
 * same handlers as NextStepCard (dialog steps link to the project page with ?step=). Above them the DSH-01 counters
 * (docs/08 "Cần xử lý") — the only place they appear on the dashboard.
 */
export function FlowTodoBlock({ summary }: { summary: Dashboard['actionRequired'] }) {
  const [showAll, setShowAll] = useState(false)
  const { data, isLoading, isError, refetch } = useFlowActions()
  const runner = useFlowActionRunner()
  const { items, hidden } = projectsNeedingAction(data ?? [], showAll ? Infinity : DASHBOARD_FLOW_LIMIT)

  return (
    <section aria-label="Việc cần làm" className={`${panelClass} space-y-4`}>
      <h2 className="flex items-center gap-2 text-base font-semibold text-foreground">
        <Inbox className="h-[18px] w-[18px] text-primary" aria-hidden="true" />
        Việc cần làm
      </h2>

      <ul aria-label="Cần xử lý" className="flex flex-wrap gap-2">
        <Counter to={dashboardLinks.newRequests} label="Yêu cầu mới" value={summary.newRequests} testId="dsh-newRequests" />
        <Counter to={dashboardLinks.overdueTasks} label="Task quá hạn" value={summary.overdueTasks} testId="dsh-overdueTasks" alert={summary.overdueTasks > 0} />
        <Counter to={dashboardLinks.dueTodayTasks} label="Task đến hạn hôm nay" value={summary.dueTodayTasks} testId="dsh-dueTodayTasks" />
        <Counter to={dashboardLinks.awaitingQuotations} label="Báo giá chờ phản hồi" value={summary.awaitingQuotations} testId="dsh-awaitingQuotations" />
        <Counter to={dashboardLinks.expiringSoonQuotations} label="Báo giá hết hạn trong 3 ngày" value={summary.expiringSoonQuotations} testId="dsh-expiringSoonQuotations" alert={summary.expiringSoonQuotations > 0} />
      </ul>

      {isLoading ? (
        <div className="space-y-3">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      ) : isError ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được việc cần làm.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : items.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không có dự án nào cần làm ngay.</p>
      ) : (
        <ul data-testid="dsh-flow" className="divide-y">
          {items.map(p => (
            <li key={p.projectId} data-testid={`dsh-flow-${p.projectId}`} className="space-y-2 py-3 first:pt-0">
              <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
                <Link
                  to={`/admin/projects/${p.projectId}`}
                  className="rounded-sm font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {p.projectName}
                </Link>
                <span className="text-xs text-muted-foreground">{p.clientName} · {stageLabel[p.stage]}</span>
              </div>
              {p.warnings.length > 0 && (
                <ul aria-label="Cảnh báo" className="flex flex-wrap gap-1.5">
                  {p.warnings.map((w, i) => <li key={`${w.kind}-${i}`}><FlowWarningChip warning={w} /></li>)}
                </ul>
              )}
              {p.actions.length > 0 && (
                <div className="flex flex-wrap items-center gap-2">
                  {p.actions.map(a => (
                    <FlowActionButton
                      key={flowActionKey(a)}
                      action={a}
                      projectId={p.projectId}
                      busy={runner.busy}
                      onRun={runner.run}
                      dialogsAsLinks
                      compact
                    />
                  ))}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {hidden > 0 && (
        <Button variant="link" className="px-0" onClick={() => setShowAll(true)}>
          Hiện thêm {hidden} dự án
        </Button>
      )}
    </section>
  )
}
