import { useNavigate } from 'react-router-dom'
import { CalendarRange } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import type { ProjectListItem, ProjectStatus } from '@/lib/types'

const statusColor: Record<ProjectStatus, string> = {
  DRAFT: 'var(--muted-foreground)',
  QUOTED: 'var(--border-strong)',
  IN_PROGRESS: 'var(--foreground)',
  ON_HOLD: 'var(--warning)',
  COMPLETED: 'var(--success)',
  CANCELLED: 'var(--danger)',
}

const statusLabel: Record<ProjectStatus, string> = {
  DRAFT: 'Nháp',
  QUOTED: 'Đã báo giá',
  IN_PROGRESS: 'Đang làm',
  ON_HOLD: 'Tạm dừng',
  COMPLETED: 'Hoàn thành',
  CANCELLED: 'Đã huỷ',
}

const ROW_H = 36
const LABEL_W = 180
const MIN_BAR_W = 6
const PADDING_Y = 8

function parseDate(s: string | null): Date | null {
  if (!s) return null
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y, m - 1, d)
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d)
  r.setDate(r.getDate() + n)
  return r
}

interface Props {
  projects: ProjectListItem[]
}

export function ProjectsGantt({ projects }: Props) {
  const navigate = useNavigate()

  const today = new Date()
  today.setHours(0, 0, 0, 0)

  // filter projects that have at least one date
  const datable = projects.filter(p => p.startDate || p.dueDate)
  const undatedCount = projects.length - datable.length

  if (datable.length === 0) {
    return (
      <EmptyState
        icon={CalendarRange}
        title="Chưa có dự án nào có ngày bắt đầu / deadline"
        description="Thêm ngày cho dự án để xem trên timeline."
      />
    )
  }

  // compute timeline range
  const allDates: Date[] = []
  for (const p of datable) {
    if (p.startDate) allDates.push(parseDate(p.startDate)!)
    if (p.dueDate) allDates.push(parseDate(p.dueDate)!)
  }
  allDates.push(today)
  const minDate = addDays(new Date(Math.min(...allDates.map(d => d.getTime()))), -3)
  const maxDate = addDays(new Date(Math.max(...allDates.map(d => d.getTime()))), 5)

  const totalDays = Math.round((maxDate.getTime() - minDate.getTime()) / 86400000)
  const svgW = 900
  const chartW = svgW - LABEL_W
  const pxPerDay = chartW / totalDays

  function xOf(date: Date): number {
    return LABEL_W + ((date.getTime() - minDate.getTime()) / 86400000) * pxPerDay
  }

  // month ticks
  const monthTicks: { x: number; label: string }[] = []
  const cursor = new Date(minDate.getFullYear(), minDate.getMonth(), 1)
  while (cursor <= maxDate) {
    if (cursor >= minDate) {
      const x = xOf(cursor)
      const label = cursor.toLocaleDateString('vi-VN', { month: 'short', year: '2-digit' })
      monthTicks.push({ x, label })
    }
    cursor.setMonth(cursor.getMonth() + 1)
  }

  const svgH = PADDING_Y * 2 + 24 + datable.length * ROW_H

  const todayX = xOf(today)

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
        {(Object.keys(statusColor) as ProjectStatus[]).map(s => (
          <span key={s} className="inline-flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-sm" style={{ background: statusColor[s] }} aria-hidden="true" />
            {statusLabel[s]}
          </span>
        ))}
        {undatedCount > 0 && (
          <span className="sm:ml-auto">{undatedCount} dự án chưa có ngày không hiển thị trên timeline</span>
        )}
      </div>
      <div className="overflow-x-auto">
        <svg width={svgW} height={svgH} className="select-none" role="img" aria-label="Timeline dự án">
          {/* header row */}
          {monthTicks.map((t, i) => (
            <g key={i}>
              <line x1={t.x} y1={PADDING_Y + 20} x2={t.x} y2={svgH} strokeWidth={1} style={{ stroke: 'var(--border)' }} />
              <text x={t.x + 4} y={PADDING_Y + 14} fontSize={11} style={{ fill: 'var(--muted-foreground)' }}>{t.label}</text>
            </g>
          ))}

          {/* today line */}
          {todayX >= LABEL_W && todayX <= svgW && (
            <>
              <line x1={todayX} y1={PADDING_Y + 20} x2={todayX} y2={svgH} strokeWidth={1.5} strokeDasharray="4,3" style={{ stroke: 'var(--highlight-text)' }} />
              <text x={todayX + 3} y={PADDING_Y + 14} fontSize={10} fontWeight={600} style={{ fill: 'var(--highlight-text)' }}>Hôm nay</text>
            </>
          )}

          {datable.map((p, i) => {
            const y = PADDING_Y + 24 + i * ROW_H
            const start = parseDate(p.startDate) ?? parseDate(p.dueDate)!
            const end = parseDate(p.dueDate) ?? parseDate(p.startDate)!

            const x1 = xOf(start)
            const x2 = Math.max(x1 + MIN_BAR_W, xOf(end))
            const barW = x2 - x1

            const isOverdue =
              p.dueDate &&
              p.dueDate < today.toISOString().slice(0, 10) &&
              p.status !== 'COMPLETED' &&
              p.status !== 'CANCELLED'

            const fillColor = isOverdue ? 'var(--highlight)' : statusColor[p.status]

            return (
              <g key={p.id} onClick={() => navigate(`/admin/projects/${p.id}`)} style={{ cursor: 'pointer' }}>
                {/* zebra */}
                <rect x={0} y={y - 2} width={svgW} height={ROW_H} style={{ fill: i % 2 === 0 ? 'transparent' : 'var(--surface-sunk)' }} />
                {/* label */}
                <text x={4} y={y + ROW_H / 2 + 4} fontSize={12} style={{ fill: 'var(--foreground)' }} className="truncate">
                  {p.name.length > 22 ? p.name.slice(0, 20) + '…' : p.name}
                </text>
                {/* bar */}
                <rect
                  x={x1}
                  y={y + 6}
                  width={barW}
                  height={ROW_H - 14}
                  rx={3}
                  style={{ fill: fillColor }}
                  opacity={p.status === 'CANCELLED' ? 0.4 : 0.85}
                />
              </g>
            )
          })}
        </svg>
      </div>
    </div>
  )
}
