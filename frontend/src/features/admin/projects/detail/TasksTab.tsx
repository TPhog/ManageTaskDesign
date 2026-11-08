import { useState } from 'react'
import { List, ListChecks, SquareKanban } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { cn } from '@/lib/utils'
import type { ProjectDetail } from '@/lib/types'
import { QuickCreateTask } from '@/features/admin/tasks/QuickCreateTask'
import { TaskBoard } from '@/features/admin/tasks/TaskBoard'
import { TaskDrawer } from '@/features/admin/tasks/TaskDrawer'
import { TaskTable } from '@/features/admin/tasks/TaskTable'
import { compareForList } from '@/features/admin/tasks/taskUtils'
import { useTaskList } from '@/features/admin/tasks/useTasks'

type View = 'board' | 'list'

export function TasksTab({ project }: { project: ProjectDetail }) {
  const [view, setView] = useState<View>('board')
  const [openId, setOpenId] = useState<string | null>(null)
  const { data, isLoading, isError } = useTaskList({ projectId: project.id })
  const tasks = data?.items ?? []

  const viewButton = (mode: View, label: string, Icon: typeof List) => (
    <button
      type="button"
      onClick={() => setView(mode)}
      title={label}
      aria-label={label}
      aria-pressed={view === mode}
      className={cn(
        'inline-flex h-9 w-9 items-center justify-center rounded-md transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        view === mode ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
      )}
    >
      <Icon className="h-4 w-4" aria-hidden="true" />
    </button>
  )

  return (
    <div className="rounded-xl border bg-card shadow-card">
      <div className="flex items-center justify-between gap-3 border-b p-4">
        <p className="text-sm text-muted-foreground">{tasks.length} công việc</p>
        <div role="group" aria-label="Chế độ xem" className="flex items-center gap-1 rounded-lg bg-muted p-1">
          {viewButton('board', 'Bảng', SquareKanban)}
          {viewButton('list', 'Danh sách', List)}
        </div>
      </div>

      {isLoading ? (
        <div className="flex gap-3 p-4">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-64 w-64 shrink-0 rounded-xl" />)}
        </div>
      ) : isError ? (
        <EmptyState icon={ListChecks} title="Không tải được công việc" description="Vui lòng tải lại trang." />
      ) : view === 'board' ? (
        <TaskBoard projectId={project.id} tasks={tasks} onOpen={setOpenId} />
      ) : (
        <>
          {tasks.length === 0 ? (
            <EmptyState
              icon={ListChecks}
              title="Chưa có công việc"
              description="Thêm việc cần làm cho dự án này; ở chế độ Bảng có thể kéo thả để đổi trạng thái."
            />
          ) : (
            <TaskTable tasks={[...tasks].sort(compareForList)} onOpen={setOpenId} />
          )}
          <div className="border-t p-3">
            <QuickCreateTask projectId={project.id} label="Thêm công việc" />
          </div>
        </>
      )}

      <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />
    </div>
  )
}
