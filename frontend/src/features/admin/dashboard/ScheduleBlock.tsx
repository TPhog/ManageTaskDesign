import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarDays, Flag, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { DashboardSchedule, ScheduleDay, ScheduleDeadline, ScheduleTask } from '@/lib/dashboardTypes'
import { TaskDrawer } from '@/features/admin/tasks/TaskDrawer'
import { taskPriorityLabel } from '@/features/admin/tasks/taskUtils'
import { dayHeading, dayMonth, scheduleIsEmpty } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useCompleteScheduleTask, useDashboardSchedule } from './useDashboardBlocks'

const chipClass = 'inline-flex max-w-full items-center truncate rounded-full border px-2 py-0.5 text-xs text-muted-foreground'

function TaskRow({ task, onComplete, onOpenTask }: { task: ScheduleTask; onComplete: (t: ScheduleTask) => void; onOpenTask: (id: string) => void }) {
  return (
    <li className="flex min-h-11 items-center gap-1">
      <label className="flex h-11 w-11 shrink-0 cursor-pointer items-center justify-center">
        <input
          type="checkbox"
          checked={false}
          onChange={() => onComplete(task)}
          aria-label={`Đánh dấu xong: ${task.title}`}
          className="h-5 w-5 cursor-pointer accent-primary"
        />
      </label>
      <div className="min-w-0 flex-1 py-1">
        <button
          type="button"
          onClick={() => onOpenTask(task.id)}
          className="block max-w-full truncate rounded text-left text-sm font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {task.title}
        </button>
        <div className="mt-0.5 flex min-w-0 flex-wrap items-center gap-1.5">
          <Link to={`/admin/projects/${task.projectId}`} className={`${chipClass} hover:border-primary/40 hover:text-foreground`}>
            {task.projectName} · {task.clientName}
          </Link>
          {task.priority !== 'MEDIUM' && (
            <span className={`${chipClass} ${task.priority === 'URGENT' || task.priority === 'HIGH' ? 'border-amber-300 text-amber-800' : ''}`}>
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
    <li className="flex min-h-11 items-center gap-2 pl-3">
      <Flag className="h-4 w-4 shrink-0 text-primary" aria-hidden="true" />
      <Link
        to={`/admin/projects/${deadline.projectId}`}
        className="min-w-0 truncate text-sm text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span className="font-medium">{deadline.kind === 'PROJECT' ? 'Hạn dự án' : 'Hạn hạng mục'}</span>: {deadline.name}
        <span className="text-muted-foreground"> · {deadline.projectName}</span>
      </Link>
    </li>
  )
}

function DaySection({ day, today, onComplete, onOpenTask }: {
  day: ScheduleDay
  today: string
  onComplete: (t: ScheduleTask) => void
  onOpenTask: (id: string) => void
}) {
  const heading = dayHeading(day.date, today)
  return (
    <section aria-label={heading} className="space-y-1">
      <h3 className="text-sm font-semibold text-foreground">
        {heading}
        <span className="ml-2 text-xs font-normal text-muted-foreground">{day.tasks.length + day.hiddenTasks} việc</span>
      </h3>
      <ul className="divide-y">
        {day.tasks.map(t => <TaskRow key={t.id} task={t} onComplete={onComplete} onOpenTask={onOpenTask} />)}
        {day.deadlines.map(d => <DeadlineRow key={`${d.kind}-${d.id}`} deadline={d} />)}
      </ul>
      {day.hiddenTasks > 0 && (
        <Link to="/admin/tasks?due=week" className="inline-block py-2 text-sm text-primary underline-offset-2 hover:underline">
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
  const busy = days.filter(d => d.tasks.length > 0 || d.deadlines.length > 0 || d.hiddenTasks > 0)
  const idle = days.filter(d => !busy.includes(d))
  const showOverdue = overdue.total > 0
  const empty = scheduleIsEmpty(schedule)

  return (
    <div className="space-y-4">
      {showOverdue && (
        <section aria-label="Quá hạn" className="space-y-1 rounded-lg border border-red-200 bg-red-50/40 p-3">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-red-700">
            <TriangleAlert className="h-4 w-4" aria-hidden="true" />
            Quá hạn ({overdue.total})
          </h3>
          <ul className="divide-y divide-red-100">
            {overdue.items.map(t => <TaskRow key={t.id} task={t} onComplete={onComplete} onOpenTask={onOpenTask} />)}
          </ul>
          {overdue.total > overdue.items.length && (
            <p className="py-1 text-sm text-muted-foreground">
              và {overdue.total - overdue.items.length} việc khác —{' '}
              <Link to="/admin/tasks?due=overdue" className="text-primary underline-offset-2 hover:underline">Xem tất cả</Link>
            </p>
          )}
        </section>
      )}

      {empty ? (
        <p className="text-sm text-muted-foreground">Tuần này chưa có việc nào. Thêm hạn cho task để thấy chúng ở đây.</p>
      ) : (
        <>
          {busy.map(d => <DaySection key={d.date} day={d} today={today} onComplete={onComplete} onOpenTask={onOpenTask} />)}
          {idle.length > 0 && (
            <p className="text-sm text-muted-foreground">Không có việc: {idle.map(d => dayMonth(d.date)).join(', ')}</p>
          )}
        </>
      )}

      {undatedOpenTasks > 0 && (
        <Link to="/admin/tasks?due=all" className="inline-block text-sm text-primary underline-offset-2 hover:underline">
          {undatedOpenTasks} công việc chưa đặt hạn
        </Link>
      )}
    </div>
  )
}

/** "Lịch làm việc 7 ngày": overdue first, then today to today+6. Ticking a task marks it DONE (with undo). */
export function ScheduleBlock() {
  const { data, isLoading, error, refetch } = useDashboardSchedule()
  const complete = useCompleteScheduleTask()
  const [openTaskId, setOpenTaskId] = useState<string | null>(null)

  return (
    <Section title="Lịch làm việc 7 ngày" icon={CalendarDays}>
      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-11 w-full" />)}
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
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
