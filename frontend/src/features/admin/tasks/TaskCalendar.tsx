import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import {
  DndContext,
  DragOverlay,
  MouseSensor,
  TouchSensor,
  pointerWithin,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import { CalendarDays, ChevronLeft, ChevronRight, Flag, FolderKanban } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { useIsMobile } from '@/features/admin/dashboard/useIsMobile'
import { cn } from '@/lib/utils'
import type { CalendarMarker, CalendarTask, TaskPriority } from '@/lib/types'
import { WEEKDAY_HEADERS, barSpan, chipDay, listDay, rangeLabel, shiftAnchor, visibleRange, type CalendarUnit } from './calendarUtils'
import { taskPriorityLabel, taskStatusLabel, todayVn } from './taskUtils'
import { useRescheduleTask, useTaskCalendar } from './useTaskCalendar'

const DAY_PREFIX = 'day:'
/** Chips and deadline markers listed in one month cell before "+N nữa" */
const MONTH_CELL_TASKS = 3
const MONTH_CELL_MARKERS = 2

const priorityEdge: Record<TaskPriority, string> = {
  URGENT: 'border-l-danger',
  HIGH: 'border-l-warning',
  MEDIUM: 'border-l-border-strong',
  LOW: 'border-l-muted-foreground',
}

const dm = (iso: string) => `${iso.slice(8, 10)}/${iso.slice(5, 7)}`

interface Props {
  /** Project tab: only this project's tasks and deadlines */
  projectId?: string
  onOpen: (id: string) => void
  /** Nothing can be dragged (e.g. a cancelled project) */
  readOnly?: boolean
  /** Controlled unit (saved views); uncontrolled when omitted */
  unit?: CalendarUnit
  onUnitChange?: (unit: CalendarUnit) => void
}

/** Month/week task calendar. Tasks sit on their due date; dropping one on another day moves the due date. */
export function TaskCalendar({ projectId, onOpen, readOnly = false, unit: unitProp, onUnitChange }: Props) {
  const today = todayVn()
  const [anchor, setAnchor] = useState(today)
  const [ownUnit, setOwnUnit] = useState<CalendarUnit>('month')
  const unit = unitProp ?? ownUnit
  const setUnit = (u: CalendarUnit) => { setOwnUnit(u); onUnitChange?.(u) }
  const isMobile = useIsMobile()

  const range = visibleRange(anchor, unit)
  const { data, isLoading, isError } = useTaskCalendar(range.from, range.to, projectId)
  const reschedule = useRescheduleTask()
  const [activeId, setActiveId] = useState<string | null>(null)

  const sensors = useSensors(
    // same thresholds as the Kanban: a click still opens the drawer, a swipe still scrolls on touch
    useSensor(MouseSensor, { activationConstraint: { distance: 5 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 250, tolerance: 8 } }),
  )

  const tasks = data?.tasks ?? []
  const markers = data?.markers ?? []
  const active = tasks.find(t => t.id === activeId)
  const canDrag = (t: CalendarTask) => !readOnly && t.status !== 'DONE'
  const crossProject = !projectId

  function onDragEnd({ active, over }: DragEndEvent) {
    setActiveId(null)
    const target = over ? String(over.id) : ''
    if (!target.startsWith(DAY_PREFIX)) return
    const dueDate = target.slice(DAY_PREFIX.length)
    const task = tasks.find(t => t.id === String(active.id))
    if (task && task.dueDate !== dueDate) reschedule.mutate({ id: task.id, dueDate })
  }

  const chip = (t: CalendarTask, opts?: { showProject?: boolean; range?: boolean }) => (
    <CalendarChip key={t.id} task={t} draggable={canDrag(t)} onOpen={onOpen} showProject={opts?.showProject} showRange={opts?.range} />
  )
  const markerRow = (m: CalendarMarker) => <MarkerRow key={`${m.kind}-${m.id}`} marker={m} showProject={crossProject} />

  let body: ReactNode
  if (isLoading) {
    body = (
      <div className="grid grid-cols-7 gap-1 p-4" aria-busy="true" aria-label="Đang tải lịch">
        {Array.from({ length: unit === 'month' ? 35 : 7 }).map((_, i) => <Skeleton key={i} className={unit === 'month' ? 'h-24' : 'h-56'} />)}
      </div>
    )
  } else if (isError) {
    body = <EmptyState icon={CalendarDays} title="Không tải được lịch" description="Vui lòng tải lại trang." />
  } else if (unit === 'month') {
    body = (
      <MonthGrid days={range.days} anchor={anchor} today={today}
        renderDay={iso => {
          // a span whose due date lies past the grid is listed on its first visible day, with its dates
          const dayTasks = tasks.filter(t => listDay(t, range.days) === iso)
          const dayMarkers = markers.filter(m => m.date === iso)
          const extra = Math.max(0, dayTasks.length - MONTH_CELL_TASKS) + Math.max(0, dayMarkers.length - MONTH_CELL_MARKERS)
          return (
            <>
              {dayMarkers.slice(0, MONTH_CELL_MARKERS).map(markerRow)}
              {dayTasks.slice(0, MONTH_CELL_TASKS).map(t => chip(t, { range: t.dueDate !== iso }))}
              {extra > 0 && (
                <button
                  type="button"
                  onClick={() => { setAnchor(iso); setUnit('week') }}
                  className="min-h-7 rounded-sm px-1 text-left text-xs font-medium text-muted-foreground hover:text-foreground hover:underline"
                >
                  +{extra} nữa
                </button>
              )}
            </>
          )
        }}
      />
    )
  } else if (isMobile) {
    body = (
      <WeekAgenda days={range.days} today={today}
        renderDay={iso => (
          <>
            {markers.filter(m => m.date === iso).map(markerRow)}
            {tasks.filter(t => listDay(t, range.days) === iso).map(t => chip(t, { showProject: crossProject, range: true }))}
          </>
        )}
      />
    )
  } else {
    const spanning = tasks.filter(t => t.startDate && t.dueDate)
    body = (
      <WeekGrid days={range.days} today={today}
        bars={spanning.flatMap(t => {
          const span = barSpan(t, range.days)
          return span ? [{ task: t, ...span }] : []
        }).map(({ task, col, span }) => (
          <div key={task.id} style={{ gridColumn: `${col} / span ${span}` }} className="min-w-0 px-1">
            {chip(task, { showProject: crossProject, range: true })}
          </div>
        ))}
        renderDay={iso => (
          <>
            {markers.filter(m => m.date === iso).map(markerRow)}
            {tasks.filter(t => !(t.startDate && t.dueDate) && chipDay(t) === iso).map(t => chip(t, { showProject: crossProject }))}
          </>
        )}
      />
    )
  }

  const empty = !isLoading && !isError && tasks.length === 0 && markers.length === 0

  return (
    <div className="min-w-0">
      <div className="flex flex-wrap items-center gap-2 border-b p-4">
        <div className="flex items-center gap-1">
          <Button variant="outline" size="icon" aria-label={unit === 'month' ? 'Tháng trước' : 'Tuần trước'} onClick={() => setAnchor(shiftAnchor(anchor, unit, -1))}>
            <ChevronLeft aria-hidden="true" />
          </Button>
          <Button variant="outline" onClick={() => setAnchor(today)}>Hôm nay</Button>
          <Button variant="outline" size="icon" aria-label={unit === 'month' ? 'Tháng sau' : 'Tuần sau'} onClick={() => setAnchor(shiftAnchor(anchor, unit, 1))}>
            <ChevronRight aria-hidden="true" />
          </Button>
        </div>
        <h3 className="min-w-0 font-display text-title font-bold tabular-nums text-foreground" aria-live="polite">{rangeLabel(anchor, unit)}</h3>
        <div role="group" aria-label="Kiểu lịch" className="ml-auto flex items-center gap-1 rounded-lg bg-muted p-1">
          {(['month', 'week'] as const).map(u => (
            <button
              key={u}
              type="button"
              aria-pressed={unit === u}
              onClick={() => setUnit(u)}
              className={cn(
                'inline-flex min-h-10 items-center rounded-md px-3 text-sm font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:min-h-8',
                unit === u ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {u === 'month' ? 'Tháng' : 'Tuần'}
            </button>
          ))}
        </div>
      </div>

      {data?.truncated && (
        <p role="status" className="border-b bg-warning-soft px-4 py-2 text-sm text-foreground">
          Khoảng này có hơn 500 công việc nên lịch chỉ hiện 500 việc đầu tiên. Lọc theo dự án để xem đủ.
        </p>
      )}
      {empty && <p className="px-4 pt-3 text-sm text-muted-foreground">Chưa có công việc nào trong khoảng này.</p>}

      <DndContext
        sensors={sensors}
        collisionDetection={pointerWithin}
        onDragStart={({ active }) => setActiveId(String(active.id))}
        onDragEnd={onDragEnd}
        onDragCancel={() => setActiveId(null)}
      >
        {body}
        <DragOverlay>{active ? <ChipBody task={active} /> : null}</DragOverlay>
      </DndContext>

      <p className="px-4 pb-4 pt-1 text-xs text-muted-foreground">
        {readOnly
          ? 'Dự án đã huỷ nên không đổi hạn được trên lịch.'
          : 'Kéo công việc sang ngày khác để đổi hạn (trên điện thoại: nhấn giữ rồi kéo). Bàn phím: mở công việc và sửa ô Hạn hoàn thành.'}
      </p>
    </div>
  )
}

// ---------- layouts ----------

function MonthGrid({ days, anchor, today, renderDay }: { days: string[]; anchor: string; today: string; renderDay: (iso: string) => ReactNode }) {
  const month = anchor.slice(0, 7)
  return (
    // the grid keeps a readable width and scrolls inside the card on narrow screens; `relative` also clips the
    // absolutely positioned sr-only texts, which would otherwise widen the page
    <div className="relative overflow-x-auto p-4">
      <div className="min-w-[640px]">
        <div className="grid grid-cols-7 border-b pb-1.5">
          {WEEKDAY_HEADERS.map(d => <div key={d} className="px-1.5 text-kicker font-semibold uppercase tracking-[0.08em] text-muted-foreground">{d}</div>)}
        </div>
        <div className="grid grid-cols-7">
          {days.map(iso => (
            <DayCell key={iso} iso={iso} today={today} muted={iso.slice(0, 7) !== month} className="min-h-28 border-b border-r p-1 [&:nth-child(7n+1)]:border-l">
              <DayNumber iso={iso} today={today} />
              <div className="mt-1 flex flex-col gap-1">{renderDay(iso)}</div>
            </DayCell>
          ))}
        </div>
      </div>
    </div>
  )
}

function WeekGrid({ days, today, bars, renderDay }: { days: string[]; today: string; bars: ReactNode[]; renderDay: (iso: string) => ReactNode }) {
  return (
    <div className="p-4">
      <div className="grid grid-cols-7 border-b pb-1.5">
        {days.map((iso, i) => (
          <div key={iso} className={cn('px-1.5 text-xs font-semibold tabular-nums', iso === today ? 'text-highlight-text' : 'text-muted-foreground')}>
            {WEEKDAY_HEADERS[i]} {dm(iso)}
          </div>
        ))}
      </div>
      {/* the droppable day columns lie under the content: a drop anywhere in a column (bar area included) lands on that day */}
      <div className="relative">
        <div className="absolute inset-0 grid grid-cols-7">
          {days.map(iso => <DayCell key={iso} iso={iso} today={today} className="border-b border-r first:border-l" />)}
        </div>
        <div className="relative min-h-56 pb-2">
          {bars.length > 0 && <div className="grid grid-cols-7 gap-y-1 pt-2">{bars}</div>}
          <div className="grid grid-cols-7">
            {days.map(iso => <div key={iso} className="flex min-w-0 flex-col gap-1 px-1 pt-2">{renderDay(iso)}</div>)}
          </div>
        </div>
      </div>
    </div>
  )
}

/** Week on a phone: one row per day, so nothing needs horizontal scrolling. */
function WeekAgenda({ days, today, renderDay }: { days: string[]; today: string; renderDay: (iso: string) => ReactNode }) {
  return (
    <ol className="m-0 list-none p-4">
      {days.map((iso, i) => (
        <li key={iso}>
          <DayCell iso={iso} today={today} className="flex min-h-16 gap-3 border-b py-2">
            <p className={cn('w-14 shrink-0 pt-1 text-xs font-semibold tabular-nums', iso === today ? 'text-highlight-text' : 'text-muted-foreground')}>
              {WEEKDAY_HEADERS[i]}<br />{dm(iso)}
            </p>
            <div className="flex min-w-0 flex-1 flex-col gap-1">{renderDay(iso)}</div>
          </DayCell>
        </li>
      ))}
    </ol>
  )
}

function DayCell({ iso, today, muted = false, className, children }: { iso: string; today: string; muted?: boolean; className?: string; children?: ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: `${DAY_PREFIX}${iso}` })
  return (
    <div
      ref={setNodeRef}
      data-day={iso}
      className={cn(
        'min-w-0 transition-colors duration-150',
        muted && 'bg-surface-sunk',
        iso === today && 'bg-highlight-soft/40',
        isOver && 'bg-surface-hover ring-2 ring-inset ring-ring',
        className,
      )}
    >
      {children}
    </div>
  )
}

function DayNumber({ iso, today }: { iso: string; today: string }) {
  const isToday = iso === today
  return (
    <span
      className={cn(
        'inline-flex h-6 min-w-6 items-center justify-center rounded-full px-1 text-xs font-semibold tabular-nums',
        isToday ? 'bg-highlight text-highlight-foreground' : 'text-muted-foreground',
      )}
    >
      {Number(iso.slice(8, 10))}
      {isToday && <span className="sr-only"> (hôm nay)</span>}
    </span>
  )
}

// ---------- items ----------

function CalendarChip({ task, draggable, onOpen, showProject, showRange }: {
  task: CalendarTask
  draggable: boolean
  onOpen: (id: string) => void
  showProject?: boolean
  showRange?: boolean
}) {
  const { setNodeRef, listeners, isDragging } = useDraggable({ id: task.id, disabled: !draggable })
  return (
    <button
      ref={setNodeRef}
      type="button"
      // keyboard path: Enter opens the drawer, where "Hạn hoàn thành" changes the date
      onClick={() => onOpen(task.id)}
      data-testid={`calendar-task-${task.id}`}
      className={cn('block w-full min-w-0 rounded-sm text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', isDragging && 'opacity-40', draggable && 'cursor-grab')}
      {...listeners}
    >
      <ChipBody task={task} showProject={showProject} showRange={showRange} />
    </button>
  )
}

function ChipBody({ task, showProject, showRange }: { task: CalendarTask; showProject?: boolean; showRange?: boolean }) {
  const done = task.status === 'DONE'
  const range = showRange && task.startDate && task.dueDate && task.startDate !== task.dueDate ? `${dm(task.startDate)} → ${dm(task.dueDate)}` : null
  return (
    <span
      title={`${task.title} — ${task.projectName}`}
      className={cn(
        'block min-w-0 rounded-sm border border-l-[3px] bg-card px-1.5 py-1 text-xs leading-tight text-foreground shadow-sm',
        priorityEdge[task.priority],
        task.isOverdue && 'bg-danger-soft',
        done && 'opacity-60',
      )}
    >
      <span className={cn('block truncate font-medium', done && 'line-through')}>{task.title}</span>
      {(showProject || range) && (
        <span className="block truncate text-[0.6875rem] text-muted-foreground">
          {[range, showProject ? task.projectName : null].filter(Boolean).join(' · ')}
        </span>
      )}
      <span className="sr-only">
        {` · ${taskStatusLabel[task.status]} · ưu tiên ${taskPriorityLabel[task.priority]}`}
        {task.isOverdue ? ' · quá hạn' : ''}
        {!task.dueDate && task.startDate ? ` · bắt đầu ${dm(task.startDate)}, chưa có hạn` : ''}
      </span>
    </span>
  )
}

function MarkerRow({ marker, showProject }: { marker: CalendarMarker; showProject: boolean }) {
  const isDeliverable = marker.kind === 'DELIVERABLE'
  const Icon = isDeliverable ? Flag : FolderKanban
  const text = isDeliverable
    ? `Hạn hạng mục: ${marker.title}${showProject ? ` — ${marker.projectName}` : ''}`
    : `Hạn dự án: ${marker.title}`
  return (
    <Link
      to={`/admin/projects/${marker.projectId}${isDeliverable ? '?tab=deliverables' : ''}`}
      title={text}
      className="flex min-h-6 min-w-0 items-center gap-1 rounded-sm px-1 text-[0.6875rem] font-medium text-highlight-text hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <Icon className="h-3 w-3 shrink-0" aria-hidden="true" />
      <span className="truncate">{text}</span>
    </Link>
  )
}
