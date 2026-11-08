import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ListChecks, Search } from 'lucide-react'
import { PageHeader } from '@/components/PageHeader'
import { EmptyState } from '@/components/EmptyState'
import { Pagination } from '@/components/Pagination'
import { SearchCombobox } from '@/components/SearchCombobox'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useClientOptions } from '@/lib/pickers'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { cn } from '@/lib/utils'
import type { TaskDueFilter, TaskListParams } from '@/lib/types'
import { TaskDrawer } from './TaskDrawer'
import { TaskTable } from './TaskTable'
import { TASK_STATUSES, taskStatusLabel } from './taskUtils'
import { useTaskCount, useTaskList } from './useTasks'

type QuickFilter = TaskDueFilter | 'all'

const PAGE_SIZE = 50

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
  const [qInput, setQInput] = useState('')
  const [clientId, setClientId] = useState('')
  const [clientLabel, setClientLabel] = useState('')
  const [clientSearch, setClientSearch] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebouncedValue(qInput.trim())
  const clients = useClientOptions(clientSearch, true, true)

  // due/status live in the URL so a filtered view can be linked to directly (e.g. /admin/tasks?due=overdue)
  const current = quickFilters.find(f => f.value === searchParams.get('due')) ?? quickFilters[3]
  const statusParam = searchParams.get('status')
  const status = TASK_STATUSES.find(s => s === statusParam)

  function setFilter(key: 'due' | 'status', value: string | undefined) {
    const next = new URLSearchParams(searchParams)
    if (value) next.set(key, value)
    else next.delete(key)
    setSearchParams(next, { replace: true })
    setPage(1)
  }

  const base: TaskListParams = {}
  if (status) base.status = status
  if (q) base.q = q
  if (clientId) base.clientId = clientId

  // QA D-18: how many tasks each quick filter holds, with the other filters applied
  const counts: Record<QuickFilter, number | undefined> = {
    overdue: useTaskCount({ ...base, due: 'overdue' }).data,
    today: useTaskCount({ ...base, due: 'today' }).data,
    week: useTaskCount({ ...base, due: 'week' }).data,
    all: useTaskCount(base).data,
  }

  const params: TaskListParams = { ...base, page, pageSize: PAGE_SIZE }
  if (current.value !== 'all') params.due = current.value

  const { data, isLoading, isError } = useTaskList(params)
  const tasks = data?.items ?? []
  const filtering = !!q || !!clientId

  return (
    <div className="space-y-6">
      <PageHeader title="Công việc" description="Công việc của mọi dự án (trừ dự án đã huỷ), sắp theo hạn hoàn thành." />

      <div className="rounded-xl border bg-card shadow-card">
        <div className="flex flex-col gap-3 border-b p-4 lg:flex-row lg:flex-wrap lg:items-center">
          <div role="group" aria-label="Lọc nhanh" className="flex flex-wrap items-center gap-1 rounded-lg bg-muted p-1">
            {quickFilters.map(f => (
              <button
                key={f.value}
                type="button"
                aria-pressed={current.value === f.value}
                onClick={() => setFilter('due', f.value === 'all' ? undefined : f.value)}
                className={cn(
                  'inline-flex min-h-10 items-center gap-1.5 rounded-md px-3 text-sm font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:min-h-8',
                  current.value === f.value ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {f.label}
                <span className="rounded-full bg-background/70 px-1.5 text-xs tabular-nums">{counts[f.value] ?? '…'}</span>
              </button>
            ))}
          </div>

          <div className="relative w-full lg:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              aria-label="Tìm công việc"
              placeholder="Tiêu đề hoặc tên dự án…"
              value={qInput}
              onChange={e => { setQInput(e.target.value); setPage(1) }}
              className="pl-9"
            />
          </div>

          <Select value={status ?? ALL_STATUSES} onValueChange={v => setFilter('status', v === ALL_STATUSES ? undefined : v)}>
            <SelectTrigger className="w-full lg:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_STATUSES}>Tất cả trạng thái</SelectItem>
              {TASK_STATUSES.map(s => <SelectItem key={s} value={s}>{taskStatusLabel[s]}</SelectItem>)}
            </SelectContent>
          </Select>

          <SearchCombobox
            id="tasks-client-filter"
            ariaLabel="Lọc theo khách"
            className="lg:w-56"
            value={clientId}
            selectedLabel={clientLabel}
            options={clients.options}
            loading={clients.loading}
            search={clientSearch}
            onSearchChange={setClientSearch}
            onSelect={o => { setClientId(o?.value ?? ''); setClientLabel(o?.label ?? ''); setPage(1) }}
            placeholder="Tất cả khách hàng"
            clearLabel="Tất cả khách hàng"
            searchPlaceholder="Tìm khách…"
            emptyText="Không có khách khớp"
          />

          {data && <p className="text-sm tabular-nums text-muted-foreground lg:ml-auto">{data.total} công việc</p>}
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
            title={filtering ? 'Không có công việc khớp bộ lọc' : current.emptyTitle}
            description={filtering ? 'Thử từ khoá khác hoặc bỏ lọc khách.' : current.emptyText}
            action={
              current.value === 'all' && !filtering ? (
                <Button asChild variant="outline">
                  <Link to="/admin/projects">Đến danh sách dự án</Link>
                </Button>
              ) : undefined
            }
          />
        ) : (
          <TaskTable tasks={tasks} onOpen={setOpenId} showProject />
        )}
        <Pagination page={page} pageSize={PAGE_SIZE} total={data?.total ?? 0} onPageChange={setPage} itemLabel="công việc" />
      </div>

      <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />
    </div>
  )
}
