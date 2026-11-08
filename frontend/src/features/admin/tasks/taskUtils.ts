import type { TaskListItem, TaskPriority, TaskStatus } from '@/lib/types'

/** Kanban column order (docs/08 §2.2) */
export const TASK_STATUSES: TaskStatus[] = ['TODO', 'IN_PROGRESS', 'REVIEW', 'DONE', 'BLOCKED']
export const TASK_PRIORITIES: TaskPriority[] = ['URGENT', 'HIGH', 'MEDIUM', 'LOW']

export const taskStatusLabel: Record<TaskStatus, string> = {
  TODO: 'Cần làm',
  IN_PROGRESS: 'Đang làm',
  REVIEW: 'Chờ duyệt',
  DONE: 'Xong',
  BLOCKED: 'Bị chặn',
}

export const taskPriorityLabel: Record<TaskPriority, string> = {
  LOW: 'Thấp',
  MEDIUM: 'Trung bình',
  HIGH: 'Cao',
  URGENT: 'Khẩn cấp',
}

/** Gap used when appending a card; mirrors ProjectTask.SortStep in the backend. */
export const SORT_STEP = 1024

/** dnd-kit droppable id of a Kanban column */
export const COLUMN_PREFIX = 'column:'
export const columnId = (status: TaskStatus) => `${COLUMN_PREFIX}${status}`

/** Calendar day in Vietnam as 'YYYY-MM-DD' — BR-81 compares with today(VN), not the browser's timezone. */
export function todayVn(now: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now)
  const part = (type: string) => parts.find(p => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}

/** Calendar arithmetic on 'YYYY-MM-DD' strings (UTC-based, so no DST/timezone drift). */
export function addDays(isoDate: string, days: number): string {
  const d = new Date(`${isoDate}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

function shortDate(isoDate: string, today: string): string {
  const [y, m, d] = isoDate.split('-')
  return y === today.slice(0, 4) ? `${d}/${m}` : `${d}/${m}/${y}`
}

export type DueTone = 'overdue' | 'today' | 'soon' | 'normal' | 'none'

/** Label + tone of a due date relative to today(VN). DONE tasks are never overdue (BR-81). */
export function dueInfo(dueDate: string | null, status: TaskStatus, today: string): { label: string; tone: DueTone } {
  if (!dueDate) return { label: 'Chưa có hạn', tone: 'none' }
  const date = shortDate(dueDate, today)
  if (status === 'DONE') return { label: date, tone: 'normal' }
  if (dueDate < today) return { label: `Quá hạn · ${date}`, tone: 'overdue' }
  if (dueDate === today) return { label: 'Hôm nay', tone: 'today' }
  if (dueDate === addDays(today, 1)) return { label: 'Ngày mai', tone: 'soon' }
  return { label: date, tone: 'normal' }
}

/** sortOrder for a card placed between two neighbours; either may be missing (docs/10 M6.1 midpoint insert). */
export function sortOrderBetween(before: number | undefined, after: number | undefined): number {
  if (before !== undefined && after !== undefined) return (before + after) / 2
  if (after !== undefined) return after - SORT_STEP
  if (before !== undefined) return before + SORT_STEP
  return SORT_STEP
}

export type BoardTask = Pick<TaskListItem, 'id' | 'status' | 'sortOrder'>

/** Tasks of one column, top to bottom. */
export function columnTasks<T extends BoardTask>(tasks: T[], status: TaskStatus): T[] {
  return tasks.filter(t => t.status === status).sort((a, b) => a.sortOrder - b.sortOrder)
}

/**
 * Where a dragged card lands. `overId` is a card id or a column id (`column:<STATUS>`).
 * Returns null when nothing changes.
 */
export function planMove(
  tasks: BoardTask[],
  activeId: string,
  overId: string,
): { status: TaskStatus; sortOrder: number } | null {
  const active = tasks.find(t => t.id === activeId)
  if (!active || activeId === overId) return null

  if (overId.startsWith(COLUMN_PREFIX)) {
    const status = overId.slice(COLUMN_PREFIX.length) as TaskStatus
    if (status === active.status) return null
    return { status, sortOrder: sortOrderBetween(columnTasks(tasks, status).at(-1)?.sortOrder, undefined) }
  }

  const over = tasks.find(t => t.id === overId)
  if (!over) return null
  const original = columnTasks(tasks, over.status)
  const column = original.filter(t => t.id !== activeId)
  const overIndex = column.findIndex(t => t.id === overId)
  // Dragging downwards inside one column lands below the hovered card (dnd-kit sortable semantics)
  const movingDown =
    active.status === over.status &&
    original.findIndex(t => t.id === activeId) < original.findIndex(t => t.id === overId)
  const [before, after] = movingDown
    ? [column[overIndex], column[overIndex + 1]]
    : [column[overIndex - 1], column[overIndex]]
  return { status: over.status, sortOrder: sortOrderBetween(before?.sortOrder, after?.sortOrder) }
}

/** List view order: board column order, then position inside the column. */
export function compareForList(a: BoardTask, b: BoardTask): number {
  return TASK_STATUSES.indexOf(a.status) - TASK_STATUSES.indexOf(b.status) || a.sortOrder - b.sortOrder
}
