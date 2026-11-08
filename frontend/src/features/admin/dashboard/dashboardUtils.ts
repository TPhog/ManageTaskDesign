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
