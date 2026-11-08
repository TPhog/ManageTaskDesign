import type { DashboardSchedule } from '@/lib/dashboardTypes'
import type { FlowProjectActions } from '@/lib/flowTypes'

/** 'yyyy-MM' → 'T6/2026' */
export function monthLabel(month: string): string {
  const [year, m] = month.split('-')
  return `T${Number(m)}/${year}`
}

/** Bar height in % of the tallest month; a non-zero month never drops below 4% so it stays visible. */
export function barPercent(value: number, max: number): number {
  if (value <= 0 || max <= 0) return 0
  return Math.max(4, Math.round((value / max) * 100))
}

/** docs/08 §2.2 "Click từng chỉ số → danh sách đã lọc" — each list reads these query params. */
export const dashboardLinks = {
  newRequests: '/admin/requests',
  overdueTasks: '/admin/tasks?due=overdue',
  dueTodayTasks: '/admin/tasks?due=today',
  awaitingQuotations: '/admin/quotations?status=SENT',
  expiringSoonQuotations: '/admin/quotations?status=SENT',
  inProgress: '/admin/projects?status=IN_PROGRESS',
  onHold: '/admin/projects?status=ON_HOLD',
  outstanding: '/admin/quotations?status=ACCEPTED',
  acceptedThisMonth: '/admin/quotations?status=ACCEPTED',
  additionalThisMonth: '/admin/quotations?status=ACCEPTED',
} as const

/** Projects shown before "Hiện thêm". */
export const DASHBOARD_FLOW_LIMIT = 8

/**
 * "Việc cần làm": projects with something to do now (an action, or a warning-level chip such as money due),
 * in the server's order (nearest due date first), primary action first.
 */
export function projectsNeedingAction(
  list: FlowProjectActions[],
  limit: number,
): { items: FlowProjectActions[]; hidden: number } {
  const needing = list
    .filter(p => p.actions.length > 0 || p.warnings.some(w => w.severity === 'warning'))
    .map(p => ({ ...p, actions: [...p.actions].sort((a, b) => Number(b.primary) - Number(a.primary)) }))
  return { items: needing.slice(0, limit), hidden: Math.max(0, needing.length - limit) }
}

const WEEKDAY = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7']

function parseYmd(date: string): [number, number, number] {
  const [y, m, d] = date.split('-').map(Number)
  return [y, m, d]
}

/** 'dd/MM' from 'yyyy-MM-dd' without going through Date (no timezone shift). */
export function dayMonth(date: string): string {
  const [, m, d] = parseYmd(date)
  return `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}`
}

/** 'Hôm nay · 15/05' | 'Ngày mai · 16/05' | 'T4 · 17/05' */
export function dayHeading(date: string, today: string): string {
  const [ty, tm, td] = parseYmd(today)
  const tomorrow = new Date(ty, tm - 1, td + 1)
  const tomorrowKey = `${tomorrow.getFullYear()}-${String(tomorrow.getMonth() + 1).padStart(2, '0')}-${String(tomorrow.getDate()).padStart(2, '0')}`
  if (date === today) return `Hôm nay · ${dayMonth(date)}`
  if (date === tomorrowKey) return `Ngày mai · ${dayMonth(date)}`
  const [y, m, d] = parseYmd(date)
  return `${WEEKDAY[new Date(y, m - 1, d).getDay()]} · ${dayMonth(date)}`
}

/** Optimistic update: the schedule without one task (pure, returns a new object). */
export function removeTaskFromSchedule(s: DashboardSchedule, taskId: string): DashboardSchedule {
  const inOverdue = s.overdue.items.some(t => t.id === taskId)
  return {
    ...s,
    overdue: inOverdue
      ? { total: Math.max(0, s.overdue.total - 1), items: s.overdue.items.filter(t => t.id !== taskId) }
      : s.overdue,
    days: s.days.map(day => (day.tasks.some(t => t.id === taskId) ? { ...day, tasks: day.tasks.filter(t => t.id !== taskId) } : day)),
  }
}

export function scheduleIsEmpty(s: DashboardSchedule): boolean {
  return s.overdue.total === 0 && s.overdue.items.length === 0 && s.days.every(d => d.tasks.length === 0 && d.deadlines.length === 0 && d.hiddenTasks === 0)
}
