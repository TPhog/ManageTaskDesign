import { useState, type ReactNode } from 'react'
import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  closestCorners,
  pointerWithin,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type CollisionDetection,
  type DragEndEvent,
} from '@dnd-kit/core'
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { GripVertical, ListChecks, MoreHorizontal } from 'lucide-react'
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
import { TASK_STATUSES, columnId, columnTasks, planMove, taskStatusLabel, todayVn } from './taskUtils'
import { useMoveTask } from './useTasks'

// The pointer decides (card under it, else the column under it); keyboard drags have no pointer → nearest corners
const collisionDetection: CollisionDetection = args => {
  const hits = pointerWithin(args)
  return hits.length > 0 ? hits : closestCorners(args)
}

const announcements: Announcements = {
  onDragStart: () => 'Đã nhấc thẻ công việc. Dùng phím mũi tên để di chuyển.',
  onDragOver: ({ over }) => (over ? 'Thẻ đang ở vị trí mới.' : 'Thẻ không nằm trên cột nào.'),
  onDragEnd: ({ over }) => (over ? 'Đã thả thẻ.' : 'Đã thả thẻ ngoài bảng, không thay đổi.'),
  onDragCancel: () => 'Đã huỷ di chuyển thẻ.',
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
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const today = todayVn()
  const active = tasks.find(t => t.id === activeId)

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
      <div className="flex gap-3 overflow-x-auto p-4">
        {TASK_STATUSES.map(status => (
          <BoardColumn
            key={status}
            projectId={projectId}
            status={status}
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
  tasks: TaskListItem[]
  today: string
  onOpen: (id: string) => void
  onMoveTo: (id: string, status: TaskStatus) => void
}

function BoardColumn({ projectId, status, tasks, today, onOpen, onMoveTo }: ColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: columnId(status) })
  const label = taskStatusLabel[status]

  return (
    <section
      ref={setNodeRef}
      aria-label={`Cột ${label}`}
      className={cn(
        'flex min-h-[24rem] w-64 shrink-0 flex-col gap-2 rounded-xl bg-muted/60 p-2.5 transition-shadow duration-150',
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
            className="-ml-1 mt-0.5 cursor-grab rounded-sm text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <GripVertical className="h-4 w-4" aria-hidden="true" />
          </button>
        }
        menu={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="-mr-1 -mt-1 h-7 w-7 shrink-0" aria-label={`Tuỳ chọn ${task.title}`}>
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
    <div className="rounded-lg border bg-card p-3 shadow-sm">
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
      </div>
      {task.deliverableName && <p className="mt-2 truncate text-xs text-muted-foreground">{task.deliverableName}</p>}
    </div>
  )
}
