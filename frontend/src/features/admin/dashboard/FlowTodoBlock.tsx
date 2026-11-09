import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Inbox, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tile, TileHeader } from '@/components/Tile'
import { FlowActionButton, FlowWarningChip } from '@/features/admin/flow/FlowActionControls'
import { flowActionKey, stageLabel } from '@/features/admin/flow/flowUtils'
import { useFlowActions } from '@/features/admin/flow/useFlow'
import { useFlowActionRunner } from '@/features/admin/flow/useFlowActionRunner'
import { DASHBOARD_FLOW_LIMIT, DASHBOARD_FLOW_LIMIT_MOBILE, projectsNeedingAction } from './dashboardUtils'
import { useIsMobile } from './useIsMobile'

/**
 * Spec 06b §6 dashboard "Việc cần làm": next steps of every open project from GET /api/admin/flow/actions, run with the
 * same handlers as NextStepCard (dialog steps link to the project page with ?step=). The DSH-01 counters live in TodayTile.
 * From `sm` a row reads like a ledger line: project + warnings on the left, its steps on the right.
 */
export function FlowTodoBlock() {
  const [showAll, setShowAll] = useState(false)
  const mobile = useIsMobile()
  const { data, isLoading, isError, refetch } = useFlowActions()
  const runner = useFlowActionRunner()
  const { items, hidden } = projectsNeedingAction(data ?? [], showAll ? Infinity : mobile ? DASHBOARD_FLOW_LIMIT_MOBILE : DASHBOARD_FLOW_LIMIT)

  return (
    <Tile aria-label="Việc cần làm" className="space-y-4">
      <TileHeader
        title="Việc cần làm"
        icon={Inbox}
        meta={!isLoading && !isError && items.length > 0 ? <span className="tabular-nums">{items.length + hidden} dự án có bước tiếp theo</span> : undefined}
      />

      {isLoading ? (
        <div className="space-y-3">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      ) : isError ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được việc cần làm.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : items.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không có dự án nào cần làm ngay.</p>
      ) : (
        <ul data-testid="dsh-flow" className="divide-y divide-border border-t border-border">
          {items.map(p => (
            <li
              key={p.projectId}
              data-testid={`dsh-flow-${p.projectId}`}
              className="space-y-2 py-3 sm:grid sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:gap-x-4 sm:gap-y-1.5 sm:space-y-0"
            >
              <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 sm:col-start-1">
                <Link
                  to={`/admin/projects/${p.projectId}`}
                  className="min-w-0 break-words rounded-sm font-display font-bold text-foreground link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {p.projectName}
                </Link>
                <span className="text-xs text-muted-foreground">{p.clientName} · {stageLabel[p.stage]}</span>
              </div>
              {p.warnings.length > 0 && (
                <ul aria-label="Cảnh báo" className="flex flex-wrap gap-1.5 sm:col-start-1">
                  {p.warnings.map((w, i) => <li key={`${w.kind}-${i}`}><FlowWarningChip warning={w} /></li>)}
                </ul>
              )}
              {p.actions.length > 0 && (
                <div className="flex flex-wrap items-center gap-2 sm:col-start-2 sm:row-span-2 sm:row-start-1 sm:max-w-80 sm:justify-end">
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
    </Tile>
  )
}
