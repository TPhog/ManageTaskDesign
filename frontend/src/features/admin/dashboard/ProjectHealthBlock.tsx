import { Link } from 'react-router-dom'
import { Activity, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { ProjectHealth, ProjectHealthItem } from '@/lib/dashboardTypes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { stageLabel } from '@/features/admin/flow/flowUtils'
import { deadlineLabel, RISK_LABEL, SEVERE_RISKS } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useProjectHealth } from './useDashboardBlocks'

function RiskChip({ risk }: { risk: ProjectHealthItem['risks'][number] }) {
  const severe = SEVERE_RISKS.includes(risk)
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium',
        severe ? 'border-red-300 bg-red-50 text-red-700' : 'border-amber-300 bg-amber-50 text-amber-800',
      )}
    >
      <TriangleAlert className="h-3 w-3" aria-hidden="true" />
      {RISK_LABEL[risk]}
    </span>
  )
}

function quotaText(used: number, total: number): string {
  return total === 0 ? 'Không có lượt sửa miễn phí' : `${used}/${total} lượt sửa`
}

function HealthRow({ item }: { item: ProjectHealthItem }) {
  const late = item.daysToDeadline !== null && item.daysToDeadline < 0
  return (
    <li className="space-y-2 py-3">
      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-start gap-x-3">
        <div className="min-w-0">
          <Link
            to={`/admin/projects/${item.projectId}`}
            className="block truncate text-sm font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {item.projectName}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{item.clientName} · {stageLabel[item.stage]}</span>
        </div>
        <div className="text-right">
          <span className={cn('block text-sm font-medium tabular-nums', late ? 'text-red-700' : 'text-foreground')}>
            {deadlineLabel(item.daysToDeadline)}
          </span>
          {item.dueDate && <span className="block text-xs tabular-nums text-muted-foreground">{formatDate(item.dueDate)}</span>}
        </div>
      </div>

      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1">
        {item.progressPercent === null ? (
          <span className="text-xs text-muted-foreground">Chưa có task</span>
        ) : (
          <div className="flex items-center gap-2">
            <div
              role="progressbar"
              aria-label={`Tiến độ ${item.projectName}`}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={item.progressPercent}
              className="h-2 flex-1 overflow-hidden rounded-full bg-muted"
            >
              <div className="h-full bg-primary" style={{ width: `${item.progressPercent}%` }} />
            </div>
            <span className="w-10 text-right text-xs tabular-nums text-muted-foreground">{item.progressPercent}%</span>
          </div>
        )}
        <span className="text-xs text-muted-foreground">{quotaText(item.quotaUsed, item.quotaTotal)}</span>
      </div>

      {item.risks.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {item.risks.map(r => <RiskChip key={r} risk={r} />)}
        </div>
      )}
    </li>
  )
}

/** Presentational: projects ordered by risk, worst first (server order). */
export function ProjectHealthView({ health }: { health: ProjectHealth }) {
  if (health.items.length === 0) {
    return <p className="text-sm text-muted-foreground">Chưa có dự án nào đang chạy.</p>
  }
  return (
    <div>
      <ul className="divide-y">
        {health.items.map(i => <HealthRow key={i.projectId} item={i} />)}
      </ul>
      {health.hidden > 0 && (
        <Link to="/admin/projects" className="inline-block py-2 text-sm text-primary underline-offset-2 hover:underline">
          và {health.hidden} dự án khác
        </Link>
      )}
    </div>
  )
}

export function ProjectHealthBlock() {
  const { data, isLoading, error, refetch } = useProjectHealth()
  return (
    <Section title="Sức khỏe dự án" icon={Activity}>
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được sức khỏe dự án.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <ProjectHealthView health={data} />
      )}
    </Section>
  )
}
