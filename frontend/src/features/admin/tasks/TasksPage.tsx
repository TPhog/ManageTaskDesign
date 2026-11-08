import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ListChecks } from 'lucide-react'
import { PageHeader } from '@/components/PageHeader'
import { EmptyState } from '@/components/EmptyState'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import type { TaskDueFilter, TaskListParams } from '@/lib/types'
import { TaskDrawer } from './TaskDrawer'
import { TaskTable } from './TaskTable'
import { TASK_STATUSES, taskStatusLabel } from './taskUtils'
import { useTaskList } from './useTasks'

type QuickFilter = TaskDueFilter | 'all'

const quickFilters: { value: QuickFilter; label: string; emptyTitle: string; emptyText: string }[] = [
  { value: 'overdue', label: 'Quá hạn', emptyTitle: 'Không có công việc quá hạn', emptyText: 'Bạn đang đúng tiến độ.' },
  { value: 'today', label: 'Hôm nay', emptyTitle: 'Không có công việc đến hạn hôm nay', emptyText: 'Xem tab Tuần này để chuẩn bị trước.' },
  { value: 'week', label: 'Tuần này', emptyTitle: 'Tuần này không có công việc đến hạn', emptyText: 'Tính từ hôm nay đến Chủ nhật.' },
  { value: 'all', label: 'Tất cả', emptyTitle: 'Chưa có công việc nào', emptyText: 'Tạo công việc trong tab Công việc của từng dự án.' },
]

// Radix Select reserves '' for "no value"
const ALL_STATUSES = '_all'

export default function TasksPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [openId, setOpenId] = useState<string | null>(null)

  // Filters live in the URL so a filtered view can be linked to directly (e.g. /admin/tasks?due=overdue)
  const current = quickFilters.find(f => f.value === searchParams.get('due')) ?? quickFilters[3]
  const statusParam = searchParams.get('status')
  const status = TASK_STATUSES.find(s => s === statusParam)

  function setFilter(key: 'due' | 'status', value: string | undefined) {
    const next = new URLSearchParams(searchParams)
    if (value) next.set(key, value)
    else next.delete(key)
    setSearchParams(next, { replace: true })
  }

  const params: TaskListParams = {}
  if (current.value !== 'all') params.due = current.value
  if (status) params.status = status

  const { data, isLoading, isError } = useTaskList(params)
  const tasks = data?.items ?? []

  return (
    <div className="space-y-6">
      <PageHeader title="Công việc" description="Công việc của mọi dự án đang chạy, sắp theo hạn hoàn thành." />

      <div className="rounded-xl border bg-card shadow-card">
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center">
          <div role="group" aria-label="Lọc nhanh" className="flex flex-wrap items-center gap-1 rounded-lg bg-muted p-1">
            {quickFilters.map(f => (
              <button
                key={f.value}
                type="button"
                aria-pressed={current.value === f.value}
                onClick={() => setFilter('due', f.value === 'all' ? undefined : f.value)}
                className={cn(
                  'h-8 rounded-md px-3 text-sm font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  current.value === f.value ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {f.label}
              </button>
            ))}
          </div>

          <Select value={status ?? ALL_STATUSES} onValueChange={v => setFilter('status', v === ALL_STATUSES ? undefined : v)}>
            <SelectTrigger className="w-full sm:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_STATUSES}>Tất cả trạng thái</SelectItem>
              {TASK_STATUSES.map(s => <SelectItem key={s} value={s}>{taskStatusLabel[s]}</SelectItem>)}
            </SelectContent>
          </Select>

          {data && <p className="text-sm tabular-nums text-muted-foreground sm:ml-auto">{data.total} công việc</p>}
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-11 w-full" />)}
          </div>
        ) : isError ? (
          <EmptyState icon={ListChecks} title="Không tải được công việc" description="Vui lòng tải lại trang." />
        ) : tasks.length === 0 ? (
          <EmptyState
            icon={ListChecks}
            title={current.emptyTitle}
            description={current.emptyText}
            action={
              current.value === 'all' ? (
                <Button asChild variant="outline">
                  <Link to="/admin/projects">Đến danh sách dự án</Link>
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <TaskTable tasks={tasks} onOpen={setOpenId} showProject />
            {data && data.total > tasks.length && (
              <p className="border-t px-4 py-3 text-xs text-muted-foreground">
                Đang hiển thị {tasks.length}/{data.total} công việc gần hạn nhất.
              </p>
            )}
          </>
        )}
      </div>

      <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />
    </div>
  )
}
