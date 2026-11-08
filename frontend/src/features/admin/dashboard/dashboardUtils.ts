import type { DashboardSchedule, ScheduleTask, HealthRisk, MoneyPeriod, ReceivableItem } from '@/lib/dashboardTypes'
import { paymentReminderMessage } from '@/lib/messageTemplates'
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

/** Undo of removeTaskFromSchedule for one task (failed tick): puts it back in overdue or in its day. */
export function restoreTaskInSchedule(s: DashboardSchedule, task: ScheduleTask): DashboardSchedule {
  const present = s.overdue.items.some(t => t.id === task.id) || s.days.some(d => d.tasks.some(t => t.id === task.id))
  if (present) return s
  if (task.dueDate < s.today) {
    const items = [...s.overdue.items, task].sort((a, b) => a.dueDate.localeCompare(b.dueDate))
    return { ...s, overdue: { total: s.overdue.total + 1, items } }
  }
  return {
    ...s,
    days: s.days.map(d => (d.date === task.dueDate ? { ...d, tasks: [...d.tasks, task].sort((a, b) => a.sortOrder - b.sortOrder) } : d)),
  }
}

export function scheduleIsEmpty(s: DashboardSchedule): boolean {
  return s.overdue.total === 0 && s.overdue.items.length === 0 && s.days.every(d => d.tasks.length === 0 && d.deadlines.length === 0 && d.hiddenTasks === 0)
}

export const RISK_LABEL: Record<HealthRisk, string> = {
  LATE: 'Trễ hạn',
  DEPOSIT_UNPAID: 'Chưa nhận cọc',
  BALANCE_DUE: 'Còn nợ',
  OPEN_REQUESTS: 'Có yêu cầu mới',
  TASKS_OVERDUE: 'Task quá hạn',
  QUOTA_EXCEEDED: 'Hết lượt sửa',
}

/** Risks that mean money or the deadline is at stake render red; the rest amber. */
export const SEVERE_RISKS: readonly HealthRisk[] = ['LATE', 'DEPOSIT_UNPAID', 'BALANCE_DUE']

export function deadlineLabel(days: number | null): string {
  if (days === null) return 'Chưa có hạn'
  if (days < 0) return `Trễ ${-days} ngày`
  if (days === 0) return 'Hôm nay'
  if (days === 1) return 'Ngày mai'
  return `Còn ${days} ngày`
}

export function deltaLabel(delta: number | null): string {
  if (delta === null) return 'Chưa có kỳ trước để so sánh'
  const abs = Math.abs(delta).toFixed(1).replace('.', ',')
  const sign = delta > 0 ? '+' : delta < 0 ? '−' : ''
  return `${sign}${abs}% so với cùng kỳ trước`
}

export function agingLabel(days: number): string {
  return days <= 0 ? 'Hôm nay' : `${days} ngày`
}

export const AGING_BUCKET_LABEL: Record<ReceivableItem['agingBucket'], string> = {
  '0-7': 'Dưới 8 ngày',
  '8-30': '8 đến 30 ngày',
  '31+': 'Quá 30 ngày',
}

export const PERIOD_LABEL: Record<MoneyPeriod, string> = {
  month: 'Tháng này',
  quarter: 'Quý này',
  year: 'Năm nay',
}

/** Same wording as the quotation page's "Copy nhắc thanh toán". */
export function receivableReminder(item: ReceivableItem): string {
  return paymentReminderMessage(item)
}

/** Day before the exclusive end of a window, as 'yyyy-MM-dd' (for "01/05/2030 – 15/05/2030" captions). */
export function lastDayOf(endExclusive: string): string {
  const [y, m, d] = parseYmd(endExclusive)
  const prev = new Date(y, m - 1, d - 1)
  return `${prev.getFullYear()}-${String(prev.getMonth() + 1).padStart(2, '0')}-${String(prev.getDate()).padStart(2, '0')}`
}
