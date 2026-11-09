import { useState, type ReactNode } from 'react'
import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  MouseSensor,
  TouchSensor,
  closestCorners,
  pointerWithin,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type UniqueIdentifier,
  type CollisionDetection,
  type DragEndEvent,
} from '@dnd-kit/core'
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { FileText, GripVertical, ListChecks, MessageSquare, MoreHorizontal } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import type { TaskListItem, TaskStatus } from '@/lib/types'
import { QuickCreateTask } from './QuickCreateTask'
import { DueText, PriorityBadge } from './TaskMeta'
import { COLUMN_PREFIX, TASK_STATUSES, columnId, columnTasks, planMove, taskStatusLabel, todayVn } from './taskUtils'
import { useMoveTask } from './useTasks'

// The pointer decides (card under it, else the column under it); keyboard drags have no pointer → nearest corners
const collisionDetection: CollisionDetection = args => {
  const hits = pointerWithin(args)
  return hits.length > 0 ? hits : closestCorners(args)
}

const screenReaderInstructions = {
  draggable: 'Nhấn Space hoặc Enter để nhấc thẻ, phím mũi tên để di chuyển, Space để thả, Esc để huỷ.',
}

interface Props {
  projectId: string
  tasks: TaskListItem[]
  onOpen: (id: string) => void
}

export function TaskBoard({ projectId, tasks, onOpen }: Props) {
  const move = useMoveTask()
  const [activeId, setActiveId] = useState<string | null>(null)
  const sensors = useSensors(
    // 5px threshold: a plain click on the card still opens the drawer
    useSensor(MouseSensor, { activationConstraint: { distance: 5 } }),
    // QA D-15: on touch a long press (250 ms) picks the card up, so a swipe still scrolls the board
    useSensor(TouchSensor, { activationConstraint: { delay: 250, tolerance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const today = todayVn()
  const active = tasks.find(t => t.id === activeId)

  const titleOf = (id: UniqueIdentifier) => tasks.find(t => t.id === String(id))?.title ?? 'công việc'
  // Label of the column a drop target belongs to (a column itself or a card inside one)
  const columnLabelOf = (id: UniqueIdentifier) => {
    const key = String(id)
    const status = key.startsWith(COLUMN_PREFIX)
      ? (key.slice(COLUMN_PREFIX.length) as TaskStatus)
      : tasks.find(t => t.id === key)?.status
    return status ? taskStatusLabel[status] : ''
  }
  const announcements: Announcements = {
    onDragStart: ({ active }) => `Đã nhấc «${titleOf(active.id)}». Dùng phím mũi tên để di chuyển.`,
    onDragOver: ({ active, over }) =>
      over ? `«${titleOf(active.id)}» đang ở cột ${columnLabelOf(over.id)}.` : `«${titleOf(active.id)}» không nằm trên cột nào.`,
    onDragEnd: ({ active, over }) => {
      if (!over) return `Đã thả «${titleOf(active.id)}» ngoài bảng, không thay đổi.`
      return planMove(tasks, String(active.id), String(over.id))
        ? `Đã thả «${titleOf(active.id)}» vào cột ${columnLabelOf(over.id)}.`
        : `Đã thả «${titleOf(active.id)}», không thay đổi.`
    },
    onDragCancel: ({ active }) => `Đã huỷ di chuyển «${titleOf(active.id)}», không thay đổi.`,
  }

  function moveTo(id: string, overId: string) {
    const plan = planMove(tasks, id, overId)
    if (plan) move.mutate({ id, ...plan })
  }

  function onDragEnd({ active, over }: DragEndEvent) {
    setActiveId(null)
    if (over) moveTo(String(active.id), String(over.id))
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      accessibility={{ announcements, screenReaderInstructions }}
      onDragStart={({ active }) => setActiveId(String(active.id))}
      onDragEnd={onDragEnd}
      onDragCancel={() => setActiveId(null)}
    >
      <div className="flex gap-3 overflow-x-auto p-4 lg:grid lg:grid-cols-[repeat(5,minmax(11rem,1fr))]">
        {TASK_STATUSES.map(status => (
          <BoardColumn
            key={status}
            projectId={projectId}
            status={status}
            activeStatus={active?.status}
            tasks={columnTasks(tasks, status)}
            today={today}
            onOpen={onOpen}
            onMoveTo={(id, to) => moveTo(id, columnId(to))}
          />
        ))}
      </div>
      <DragOverlay>{active ? <CardBody task={active} today={today} /> : null}</DragOverlay>
    </DndContext>
  )
}

interface ColumnProps {
  projectId: string
  status: TaskStatus
  /** Status of the card being dragged; its own column is not a drop target */
  activeStatus?: TaskStatus
  tasks: TaskListItem[]
  today: string
  onOpen: (id: string) => void
  onMoveTo: (id: string, status: TaskStatus) => void
}

function BoardColumn({ projectId, status, activeStatus, tasks, today, onOpen, onMoveTo }: ColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: columnId(status), disabled: status === activeStatus })
  const label = taskStatusLabel[status]

  return (
    <section
      ref={setNodeRef}
      aria-label={`Cột ${label}`}
      className={cn(
        'flex min-h-[24rem] w-64 min-w-[16rem] shrink-0 flex-col lg:w-auto lg:min-w-0 gap-2 rounded-xl bg-muted/60 p-2.5 transition-shadow duration-150',
        isOver && 'ring-2 ring-primary/40',
      )}
    >
      <div className="flex items-center justify-between px-1.5 pt-0.5">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{label}</h3>
        <span className="rounded-full bg-card px-2 text-xs tabular-nums text-muted-foreground">{tasks.length}</span>
      </div>
      <SortableContext id={status} items={tasks.map(t => t.id)} strategy={verticalListSortingStrategy}>
        <ul className="flex flex-col gap-2">
          {tasks.map(t => (
            <SortableCard key={t.id} task={t} today={today} onOpen={onOpen} onMoveTo={onMoveTo} />
          ))}
        </ul>
      </SortableContext>
      <QuickCreateTask projectId={projectId} status={status} label={`Thêm vào ${label}`} />
    </section>
  )
}

interface CardProps {
  task: TaskListItem
  today: string
  onOpen: (id: string) => void
  onMoveTo: (id: string, status: TaskStatus) => void
}

function SortableCard({ task, today, onOpen, onMoveTo }: CardProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } =
    useSortable({ id: task.id })

  return (
    // Pointer drag works on the whole card; the keyboard drag starts only from the grip button (activator)
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(isDragging && 'opacity-40')}
      {...listeners}
    >
      <CardBody
        task={task}
        today={today}
        onOpen={() => onOpen(task.id)}
        handle={
          <button
            ref={setActivatorNodeRef}
            type="button"
            {...attributes}
            aria-roledescription="thẻ kéo thả"
            aria-label={`Kéo thẻ ${task.title}`}
            className="-my-2.5 -ml-2.5 inline-flex h-11 w-11 shrink-0 cursor-grab touch-none items-center justify-center rounded-sm text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:pointer-fine:my-0 sm:pointer-fine:-ml-1 sm:pointer-fine:mt-0.5 sm:pointer-fine:h-5 sm:pointer-fine:w-5"
          >
            <GripVertical className="h-4 w-4" aria-hidden="true" />
          </button>
        }
        menu={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="-my-2.5 -mr-2.5 h-11 w-11 shrink-0 sm:pointer-fine:my-0 sm:pointer-fine:-mr-1 sm:pointer-fine:-mt-1 sm:pointer-fine:h-7 sm:pointer-fine:w-7" aria-label={`Tuỳ chọn ${task.title}`}>
                <MoreHorizontal aria-hidden="true" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={() => onOpen(task.id)}>Mở chi tiết</DropdownMenuItem>
              {TASK_STATUSES.filter(s => s !== task.status).map(s => (
                <DropdownMenuItem key={s} onClick={() => onMoveTo(task.id, s)}>
                  Chuyển sang {taskStatusLabel[s]}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        }
      />
    </li>
  )
}

interface CardBodyProps {
  task: TaskListItem
  today: string
  onOpen?: () => void
  handle?: ReactNode
  menu?: ReactNode
}

function CardBody({ task, today, onOpen, handle, menu }: CardBodyProps) {
  return (
    <div data-testid={`task-card-${task.id}`} className="rounded-lg border bg-card p-3 shadow-sm">
      <div className="flex items-start gap-1.5">
        {handle}
        {onOpen ? (
          <button
            type="button"
            onClick={onOpen}
            className="min-w-0 flex-1 rounded-sm text-left text-sm font-medium leading-snug text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {task.title}
          </button>
        ) : (
          <p className="min-w-0 flex-1 text-sm font-medium leading-snug text-foreground">{task.title}</p>
        )}
        {menu}
      </div>
      <div className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1">
        <PriorityBadge priority={task.priority} />
        <DueText dueDate={task.dueDate} status={task.status} today={today} />
        {task.checklistTotal > 0 && (
          <span className="inline-flex items-center gap-1 text-xs tabular-nums text-muted-foreground">
            <ListChecks className="h-3.5 w-3.5" aria-hidden="true" />
            {task.checklistDone}/{task.checklistTotal}
          </span>
        )}
        {task.hasDescription && (
          <span title="Có mô tả" className="inline-flex items-center text-muted-foreground">
            <FileText className="h-3.5 w-3.5" aria-hidden="true" />
            <span className="sr-only">Có mô tả</span>
          </span>
        )}
        {task.notesCount > 0 && (
          <span title={`${task.notesCount} ghi chú`} className="inline-flex items-center gap-1 text-xs tabular-nums text-muted-foreground">
            <MessageSquare className="h-3.5 w-3.5" aria-hidden="true" />
            {task.notesCount}
            <span className="sr-only"> ghi chú</span>
          </span>
        )}
      </div>
      {task.deliverableName && <p className="mt-2 truncate text-xs text-muted-foreground">{task.deliverableName}</p>}
    </div>
  )
}
