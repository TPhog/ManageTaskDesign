import { Link } from 'react-router-dom'
import { Activity, CheckCircle2, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { ProjectHealth, ProjectHealthItem } from '@/lib/dashboardTypes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { stageLabel } from '@/features/admin/flow/flowUtils'
import { deadlineLabel, RISK_LABEL, SEVERE_RISKS } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useIsMobile } from './useIsMobile'
import { useProjectHealth } from './useDashboardBlocks'

/** md: the list reads as a table (CSS only — it stays a `ul` for screen readers and e2e). Columns follow the DOM order. */
const columns = 'md:grid md:grid-cols-[minmax(0,2fr)_6.5rem_minmax(0,1.1fr)_7rem_minmax(0,1.2fr)] md:gap-4'

const chip = 'inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold'

function RiskChip({ risk }: { risk: ProjectHealthItem['risks'][number] }) {
  const severe = SEVERE_RISKS.includes(risk)
  return (
    <span className={cn(chip, severe ? 'bg-danger-soft text-danger' : 'bg-warning-soft text-warning')}>
      <TriangleAlert className="h-3 w-3" aria-hidden="true" />
      {RISK_LABEL[risk]}
    </span>
  )
}

function quotaText(used: number, total: number): string {
  return total === 0 ? 'Không có lượt sửa miễn phí' : `${used}/${total} lượt sửa`
}

function QuotaDots({ used, total }: { used: number; total: number }) {
  if (total <= 0 || total > 6) return null
  const full = used >= total
  return (
    <span aria-hidden="true" className="flex shrink-0 gap-[3px]">
      {Array.from({ length: total }, (_, i) => (
        <i
          key={i}
          className={cn(
            'h-2 w-2 rounded-full border-[1.5px]',
            full ? 'border-danger' : 'border-foreground',
            i < used && (full ? 'bg-danger' : 'bg-foreground'),
          )}
        />
      ))}
    </span>
  )
}

function HealthRow({ item }: { item: ProjectHealthItem }) {
  const late = item.daysToDeadline !== null && item.daysToDeadline < 0
  return (
    <li className={cn('space-y-1 py-2.5 md:items-center md:space-y-0 md:rounded-sm md:px-2 md:transition-colors md:hover:bg-surface-sunk', columns)}>
      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-start gap-x-3 md:contents">
        <div className="min-w-0">
          <Link
            to={`/admin/projects/${item.projectId}`}
            className="block truncate rounded-sm text-sm font-semibold text-foreground link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {item.projectName}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{item.clientName} · {stageLabel[item.stage]}</span>
        </div>
        <div className="text-right md:text-left">
          <span className={cn('block text-sm tabular-nums', late ? 'font-semibold text-danger' : item.daysToDeadline === null ? 'text-muted-foreground' : 'font-semibold text-foreground')}>
            {deadlineLabel(item.daysToDeadline)}
          </span>
          {item.dueDate && <span className="block text-xs tabular-nums text-muted-foreground">{formatDate(item.dueDate)}</span>}
        </div>
      </div>

      <div className="hidden grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1 sm:grid md:contents">
        {item.progressPercent === null ? (
          <span className="text-xs text-muted-foreground">Chưa có việc</span>
        ) : (
          <div className="flex items-center gap-2.5">
            <div
              role="progressbar"
              aria-label={`Tiến độ ${item.projectName}`}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={item.progressPercent}
              className="h-1.5 flex-1 overflow-hidden rounded-full bg-border"
            >
              <div className="h-full rounded-full bg-foreground" style={{ width: `${item.progressPercent}%` }} />
            </div>
            <span className="w-9 text-right text-xs font-semibold tabular-nums text-foreground">{item.progressPercent}%</span>
          </div>
        )}
        <span className="flex items-center gap-2">
          <QuotaDots used={item.quotaUsed} total={item.quotaTotal} />
          <span className="text-xs text-muted-foreground">{quotaText(item.quotaUsed, item.quotaTotal)}</span>
        </span>
      </div>

      <div className="flex flex-wrap gap-1.5">
        {item.risks.length > 0
          ? item.risks.map(r => <RiskChip key={r} risk={r} />)
          : (
            <span className={cn(chip, 'bg-success-soft text-success')}>
              <CheckCircle2 className="h-3 w-3" aria-hidden="true" />
              Ổn
            </span>
          )}
      </div>
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
      <div aria-hidden="true" className={cn('hidden border-b border-border px-2 pb-2.5 kicker text-muted-foreground', columns)}>
        <span>Dự án · giai đoạn</span><span>Hạn</span><span>Tiến độ</span><span>Lượt sửa</span><span>Tình trạng</span>
      </div>
      <ul className="divide-y divide-border">
        {health.items.map(i => <HealthRow key={i.projectId} item={i} />)}
      </ul>
      {health.hidden > 0 && (
        <Link to="/admin/projects" className="mt-2 inline-block py-2 text-sm link-inline">
          và {health.hidden} dự án khác
        </Link>
      )}
    </div>
  )
}

/** Rows shown on the dashboard; the rest is summed into "và N dự án khác". */
const HEALTH_ROWS = 8
const HEALTH_ROWS_MOBILE = 5

export function ProjectHealthBlock() {
  const { data, isLoading, error, refetch } = useProjectHealth()
  const rows = useIsMobile() ? HEALTH_ROWS_MOBILE : HEALTH_ROWS
  return (
    <Section
      title="Sức khỏe dự án"
      icon={Activity}
      meta={data && data.items.length > 0 ? <span className="tabular-nums">{data.total} đang chạy · rủi ro cao trước</span> : undefined}
    >
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được sức khỏe dự án.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <ProjectHealthView
          health={{ ...data, items: data.items.slice(0, rows), hidden: data.hidden + Math.max(0, data.items.length - rows) }}
        />
      )}
    </Section>
  )
}
