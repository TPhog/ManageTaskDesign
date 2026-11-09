import { ArrowDown, ArrowUp, ChevronsUp, Equal, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TaskListItem, TaskPriority, TaskStatus } from '@/lib/types'
import { dueInfo, taskPriorityLabel, type DueTone } from './taskUtils'

const priorityStyle: Record<TaskPriority, [string, LucideIcon]> = {
  URGENT: ['text-danger', ChevronsUp],
  HIGH: ['text-warning', ArrowUp],
  MEDIUM: ['text-muted-foreground', Equal],
  LOW: ['text-muted-foreground', ArrowDown],
}

/** Medium is the default: only worth a badge when it differs (alwaysShow keeps it in the table column). */
export function PriorityBadge({ priority, alwaysShow = false }: { priority: TaskPriority; alwaysShow?: boolean }) {
  if (priority === 'MEDIUM' && !alwaysShow) return null
  const [cls, Icon] = priorityStyle[priority]
  return (
    <span className={cn('inline-flex items-center gap-1 whitespace-nowrap text-xs font-semibold', cls)}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {taskPriorityLabel[priority]}
    </span>
  )
}

const toneClass: Record<DueTone, string> = {
  overdue: 'font-semibold text-danger',
  today: 'font-semibold text-warning',
  soon: 'text-warning',
  normal: 'text-muted-foreground',
  none: 'text-muted-foreground',
}

/** `chip`: the Kanban card's pill (danger-soft when late, warning-soft today/tomorrow, sunk otherwise) */
const chipClass: Record<DueTone, string> = {
  overdue: 'bg-danger-soft text-danger',
  today: 'bg-warning-soft text-warning',
  soon: 'bg-warning-soft text-warning',
  normal: 'bg-surface-sunk text-foreground',
  none: '',
}

export function DueText({ dueDate, status, today, chip = false, className }: {
  dueDate: string | null
  status: TaskStatus
  today: string
  chip?: boolean
  className?: string
}) {
  const { label, tone } = dueInfo(dueDate, status, today)
  if (chip && tone === 'none') return null
  return (
    <span
      className={cn(
        'whitespace-nowrap text-xs tabular-nums',
        chip ? cn('rounded-full px-2 py-0.5 font-semibold', chipClass[tone]) : toneClass[tone],
        className,
      )}
    >
      {label}
    </span>
  )
}

/** P15: "Bị chặn: <reason> · chờ "<blocker>" · đã xong" under a BLOCKED card / row title. */
export function BlockedLine({ task, className }: {
  task: Pick<TaskListItem, 'blockedReason' | 'blockedByTitle' | 'blockedByDone'>
  className?: string
}) {
  return (
    <p className={cn('break-words text-xs text-warning', className)}>
      <span className="font-medium">Bị chặn:</span> {task.blockedReason ?? 'chưa ghi lý do'}
      {task.blockedByTitle && <> · chờ “{task.blockedByTitle}”{task.blockedByDone && ' · đã xong'}</>}
    </p>
  )
}
