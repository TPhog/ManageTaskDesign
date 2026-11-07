import { useNavigate } from 'react-router-dom'
import type { ProjectListItem, ProjectStatus } from '@/lib/types'

const statusColor: Record<ProjectStatus, string> = {
  DRAFT: '#9ca3af',
  QUOTED: '#a78bfa',
  IN_PROGRESS: '#3b82f6',
  ON_HOLD: '#fb923c',
  COMPLETED: '#22c55e',
  CANCELLED: '#ef4444',
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

  if (datable.length === 0) {
    return (
      <div className="text-center py-16 text-gray-400 text-sm">
        Chưa có dự án nào có ngày bắt đầu / deadline
      </div>
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
    <div className="overflow-x-auto">
      <svg width={svgW} height={svgH} className="select-none">
        {/* header row */}
        {monthTicks.map((t, i) => (
          <g key={i}>
            <line x1={t.x} y1={PADDING_Y + 20} x2={t.x} y2={svgH} stroke="#e5e7eb" strokeWidth={1} />
            <text x={t.x + 4} y={PADDING_Y + 14} fontSize={11} fill="#9ca3af">{t.label}</text>
          </g>
        ))}

        {/* today line */}
        {todayX >= LABEL_W && todayX <= svgW && (
          <>
            <line x1={todayX} y1={PADDING_Y + 20} x2={todayX} y2={svgH} stroke="#ef4444" strokeWidth={1.5} strokeDasharray="4,3" />
            <text x={todayX + 3} y={PADDING_Y + 14} fontSize={10} fill="#ef4444">Hôm nay</text>
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

          const fillColor = isOverdue ? '#ef4444' : statusColor[p.status]

          return (
            <g key={p.id} onClick={() => navigate(`/admin/projects/${p.id}`)} style={{ cursor: 'pointer' }}>
              {/* zebra */}
              <rect x={0} y={y - 2} width={svgW} height={ROW_H} fill={i % 2 === 0 ? 'transparent' : '#f9fafb'} />
              {/* label */}
              <text x={4} y={y + ROW_H / 2 + 4} fontSize={12} fill="#374151" className="truncate">
                {p.name.length > 22 ? p.name.slice(0, 20) + '…' : p.name}
              </text>
              {/* bar */}
              <rect
                x={x1}
                y={y + 6}
                width={barW}
                height={ROW_H - 14}
                rx={3}
                fill={fillColor}
                opacity={p.status === 'CANCELLED' ? 0.4 : 0.85}
              />
            </g>
          )
        })}
      </svg>
    </div>
  )
}
