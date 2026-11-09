import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarDays, Flag, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { DashboardSchedule, ScheduleDay, ScheduleDeadline, ScheduleTask } from '@/lib/dashboardTypes'
import { cn } from '@/lib/utils'
import { TaskDrawer } from '@/features/admin/tasks/TaskDrawer'
import { taskPriorityLabel } from '@/features/admin/tasks/taskUtils'
import { dayHeading, dayMonth, scheduleIsEmpty, weekdayShort } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useCompleteScheduleTask, useDashboardSchedule } from './useDashboardBlocks'

/**
 * Below xl the week is a vertical list (busy days only). From xl it is the mockup's 8-column board:
 * overdue column + one column per day, today outlined in ink, empty days marked "Trống".
 */

const chipClass = 'inline-flex max-w-full items-center truncate rounded-full border border-border px-2 py-0.5 text-xs text-muted-foreground'

function TaskRow({ task, kicker, onComplete, onOpenTask }: {
  task: ScheduleTask
  /** Small type line above the title on the xl board ("Việc", "Hạn 12/05") */
  kicker: string
  onComplete: (t: ScheduleTask) => void
  onOpenTask: (id: string) => void
}) {
  const high = task.priority === 'URGENT' || task.priority === 'HIGH'
  return (
    <li className="flex min-h-11 items-center gap-1 xl:grid xl:min-h-0 xl:grid-cols-[auto_minmax(0,1fr)] xl:items-center xl:gap-x-1 xl:gap-y-0.5 xl:rounded-md xl:border xl:border-border xl:bg-card xl:p-1.5 xl:pr-2 xl:transition-colors xl:hover:border-foreground">
      <label className="flex h-11 w-11 shrink-0 cursor-pointer items-center justify-center xl:h-6 xl:w-6">
        <input
          type="checkbox"
          checked={false}
          onChange={() => onComplete(task)}
          aria-label={`Đánh dấu xong: ${task.title}`}
          className="h-5 w-5 cursor-pointer accent-primary xl:h-4 xl:w-4"
        />
      </label>
      <div className="min-w-0 flex-1 py-1 xl:contents">
        <span aria-hidden="true" className="hidden truncate text-[0.6875rem] font-semibold text-muted-foreground xl:block">{kicker}</span>
        <button
          type="button"
          onClick={() => onOpenTask(task.id)}
          className="block max-w-full truncate rounded text-left text-sm font-medium text-foreground link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring xl:col-span-2 xl:line-clamp-3 xl:whitespace-normal xl:text-[0.8125rem] xl:font-semibold xl:leading-snug"
        >
          {task.title}
        </button>
        <div className="mt-0.5 flex min-w-0 flex-wrap items-center gap-1.5 xl:col-span-2 xl:mt-0 xl:gap-1">
          <Link
            to={`/admin/projects/${task.projectId}`}
            className={cn(chipClass, 'hover:border-border-strong hover:text-foreground xl:block xl:rounded-none xl:border-0 xl:p-0 xl:text-[0.6875rem] xl:hover:underline')}
          >
            {task.projectName} · {task.clientName}
          </Link>
          {task.priority !== 'MEDIUM' && (
            <span className={cn(chipClass, 'xl:px-1.5 xl:text-[0.6875rem]', high && 'border-warning/40 font-medium text-warning')}>
              {taskPriorityLabel[task.priority]}
            </span>
          )}
        </div>
      </div>
    </li>
  )
}

function DeadlineRow({ deadline }: { deadline: ScheduleDeadline }) {
  return (
    <li className="flex min-h-11 items-center gap-2 border-l-[3px] border-highlight pl-2.5 xl:min-h-0 xl:rounded-md xl:border xl:border-l-[3px] xl:border-border xl:border-l-highlight xl:bg-card xl:p-1.5 xl:pl-2">
      <Flag className="h-4 w-4 shrink-0 text-highlight-text xl:hidden" aria-hidden="true" />
      <Link
        to={`/admin/projects/${deadline.projectId}`}
        className="min-w-0 truncate rounded-sm text-sm text-foreground link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring xl:whitespace-normal xl:text-[0.8125rem] xl:leading-snug"
      >
        <span className="font-medium text-highlight-text xl:flex xl:items-center xl:gap-1 xl:text-[0.6875rem] xl:font-semibold">
          <Flag className="hidden h-3 w-3 shrink-0 xl:block" aria-hidden="true" />
          {deadline.kind === 'PROJECT' ? 'Hạn dự án' : 'Hạn hạng mục'}
        </span>
        <span className="xl:sr-only">: </span>
        <span className="xl:block xl:font-semibold">{deadline.name}</span>
        <span className="text-muted-foreground xl:block xl:text-[0.6875rem]"><span className="xl:sr-only"> · </span>{deadline.projectName}</span>
      </Link>
    </li>
  )
}

/** xl: a busy day scrolls inside its column so one heavy day does not stretch the whole week */
const listClass = 'divide-y divide-border xl:max-h-96 xl:space-y-1.5 xl:divide-y-0 xl:overflow-y-auto xl:[scrollbar-width:thin]'
/** …and fades out at the bottom edge when it holds more than fits, so the cut reads as "scroll for more" */
const fadeClass = 'xl:[mask-image:linear-gradient(to_bottom,black_calc(100%-2rem),transparent)]'
const LONG_LIST = 4
const columnClass = 'xl:flex xl:min-h-44 xl:min-w-0 xl:flex-col xl:gap-1.5 xl:space-y-0 xl:rounded-lg xl:border xl:p-2.5'

function DaySection({ day, today, idle, onComplete, onOpenTask }: {
  day: ScheduleDay
  today: string
  idle: boolean
  onComplete: (t: ScheduleTask) => void
  onOpenTask: (id: string) => void
}) {
  const heading = dayHeading(day.date, today)
  const isToday = day.date === today
  return (
    <section
      aria-label={heading}
      className={cn(
        'space-y-1',
        columnClass,
        isToday ? 'xl:border-[1.5px] xl:border-foreground xl:bg-card' : 'xl:border-border xl:bg-surface-sunk',
        idle && 'hidden',
      )}
    >
      <h3 className="kicker text-muted-foreground xl:flex xl:min-h-6 xl:items-center xl:justify-between xl:gap-1 xl:px-0.5 xl:pb-1 xl:tracking-[0.06em]">
        <span className="xl:sr-only">{heading}</span>
        <span className="ml-2 text-xs font-normal normal-case tracking-normal xl:sr-only">{day.tasks.length + day.hiddenTasks} việc</span>
        <span aria-hidden="true" className="hidden whitespace-nowrap xl:inline">
          {isToday
            ? <span className="rounded-full bg-foreground px-1.5 py-0.5 text-[0.625rem] tracking-[0.04em] text-background">Hôm nay</span>
            : weekdayShort(day.date)}
        </span>
        <span aria-hidden="true" className="hidden font-display text-xl font-bold normal-case leading-none tracking-[-0.02em] text-foreground xl:inline">
          {day.date.slice(8)}
        </span>
      </h3>
      {idle ? (
        <p className="m-auto hidden w-full rounded-md border border-dashed border-border-strong px-2 py-3 text-center text-xs text-muted-foreground xl:block">Trống</p>
      ) : (
        <ul className={cn(listClass, day.tasks.length + day.deadlines.length > LONG_LIST && fadeClass)}>
          {day.tasks.map(t => <TaskRow key={t.id} task={t} kicker="Việc" onComplete={onComplete} onOpenTask={onOpenTask} />)}
          {day.deadlines.map(d => <DeadlineRow key={`${d.kind}-${d.id}`} deadline={d} />)}
        </ul>
      )}
      {day.hiddenTasks > 0 && (
        <Link to="/admin/tasks?due=week" className="inline-block py-2 text-sm link-inline xl:px-0.5 xl:py-1 xl:text-xs">
          và {day.hiddenTasks} việc khác
        </Link>
      )}
    </section>
  )
}

/** Presentational: the 7-day work schedule (no data fetching, testable on its own). */
export function ScheduleView({ schedule, onComplete, onOpenTask }: {
  schedule: DashboardSchedule
  onComplete: (task: ScheduleTask) => void
  onOpenTask: (taskId: string) => void
}) {
  const { overdue, days, today, undatedOpenTasks } = schedule
  const isIdle = (d: ScheduleDay) => d.tasks.length === 0 && d.deadlines.length === 0 && d.hiddenTasks === 0
  const idle = days.filter(isIdle)
  const showOverdue = overdue.total > 0
  const empty = scheduleIsEmpty(schedule)

  const overdueBlock = showOverdue && (
    <section aria-label="Quá hạn" className={cn('space-y-1 rounded-lg border border-danger/30 bg-danger-soft p-3', columnClass, 'xl:border-danger/30')}>
      <h3 className="flex items-center gap-1.5 kicker text-danger xl:min-h-6 xl:whitespace-nowrap xl:px-0.5 xl:pb-1 xl:tracking-[0.06em]">
        <TriangleAlert className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        Quá hạn ({overdue.total})
      </h3>
      <ul className={cn(listClass, 'divide-danger/20', overdue.items.length > LONG_LIST && fadeClass)}>
        {overdue.items.map(t => <TaskRow key={t.id} task={t} kicker={`Hạn ${dayMonth(t.dueDate)}`} onComplete={onComplete} onOpenTask={onOpenTask} />)}
      </ul>
      {overdue.total > overdue.items.length && (
        <p className="py-1 text-sm text-muted-foreground xl:px-0.5 xl:text-xs">
          và {overdue.total - overdue.items.length} việc khác —{' '}
          <Link to="/admin/tasks?due=overdue" className="link-inline">Xem tất cả</Link>
        </p>
      )}
    </section>
  )

  return (
    <div className="space-y-4">
      {empty ? (
        <>
          {overdueBlock}
          <p className="text-sm text-muted-foreground">Tuần này chưa có việc nào. Thêm hạn cho task để thấy chúng ở đây.</p>
        </>
      ) : (
        <>
          <div className={cn('space-y-4 xl:grid xl:items-stretch xl:gap-2 xl:space-y-0', showOverdue ? 'xl:grid-cols-[minmax(0,1.15fr)_repeat(7,minmax(0,1fr))]' : 'xl:grid-cols-7')}>
            {overdueBlock}
            {days.map(d => <DaySection key={d.date} day={d} today={today} idle={isIdle(d)} onComplete={onComplete} onOpenTask={onOpenTask} />)}
          </div>
          {idle.length > 0 && (
            <p className="text-sm text-muted-foreground xl:hidden">Không có việc: {idle.map(d => dayMonth(d.date)).join(', ')}</p>
          )}
        </>
      )}

      {undatedOpenTasks > 0 && (
        <Link to="/admin/tasks?due=all" className="inline-block text-sm link-inline">
          {undatedOpenTasks} công việc chưa đặt hạn
        </Link>
      )}
    </div>
  )
}

const legend = (
  <span aria-hidden="true" className="hidden items-center gap-3 xl:inline-flex">
    <span className="inline-flex items-center gap-1.5"><i className="h-2.5 w-2.5 rounded-[3px] bg-foreground" />Công việc</span>
    <span className="inline-flex items-center gap-1.5"><i className="h-2.5 w-2.5 rounded-[3px] bg-highlight" />Hạn dự án</span>
  </span>
)

/** "Lịch làm việc 7 ngày": overdue first, then today to today+6. Ticking a task marks it DONE (with undo). */
export function ScheduleBlock() {
  const { data, isLoading, error, refetch } = useDashboardSchedule()
  const complete = useCompleteScheduleTask()
  const [openTaskId, setOpenTaskId] = useState<string | null>(null)

  return (
    <Section
      title="Lịch làm việc 7 ngày"
      icon={CalendarDays}
      meta={legend}
      action={<Link to="/admin/tasks" className="text-sm link-inline">Mở bảng công việc</Link>}
    >
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-11 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được lịch làm việc.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <ScheduleView schedule={data} onComplete={task => complete.mutate({ task })} onOpenTask={setOpenTaskId} />
      )}
      <TaskDrawer taskId={openTaskId} onClose={() => setOpenTaskId(null)} />
    </Section>
  )
}
