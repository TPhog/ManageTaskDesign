import { ArrowDown, ArrowUp, ChevronsUp, Equal, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TaskPriority, TaskStatus } from '@/lib/types'
import { dueInfo, taskPriorityLabel, type DueTone } from './taskUtils'

const priorityStyle: Record<TaskPriority, [string, LucideIcon]> = {
  URGENT: ['text-red-700', ChevronsUp],
  HIGH: ['text-orange-700', ArrowUp],
  MEDIUM: ['text-slate-600', Equal],
  LOW: ['text-sky-700', ArrowDown],
}

export function PriorityBadge({ priority }: { priority: TaskPriority }) {
  const [cls, Icon] = priorityStyle[priority]
  return (
    <span className={cn('inline-flex items-center gap-1 whitespace-nowrap text-xs font-medium', cls)}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {taskPriorityLabel[priority]}
    </span>
  )
}

const toneClass: Record<DueTone, string> = {
  overdue: 'font-medium text-red-700',
  today: 'font-medium text-amber-700',
  soon: 'text-amber-700',
  normal: 'text-muted-foreground',
  none: 'text-muted-foreground',
}

export function DueText({ dueDate, status, today }: { dueDate: string | null; status: TaskStatus; today: string }) {
  const { label, tone } = dueInfo(dueDate, status, today)
  return <span className={cn('whitespace-nowrap text-xs tabular-nums', toneClass[tone])}>{label}</span>
}
