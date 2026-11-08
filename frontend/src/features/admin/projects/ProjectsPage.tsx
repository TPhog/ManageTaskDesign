import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Plus, List, GanttChartSquare, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { PagedResult, ProjectListItem } from '@/lib/types'
import { ProjectsTable } from './ProjectsTable'
import { ProjectsGantt } from './ProjectsGantt'
import { CreateProjectDialog } from './CreateProjectDialog'

type ViewMode = 'list' | 'gantt'

// Radix Select reserves '' for "no value", so "all" uses a sentinel.
const ALL = '_all'

const statusOptions: { value: string; label: string }[] = [
  { value: ALL, label: 'Tất cả trạng thái' },
  { value: 'DRAFT', label: 'Nháp' },
  { value: 'QUOTED', label: 'Đã báo giá' },
  { value: 'IN_PROGRESS', label: 'Đang làm' },
  { value: 'ON_HOLD', label: 'Tạm dừng' },
  { value: 'COMPLETED', label: 'Hoàn thành' },
  { value: 'CANCELLED', label: 'Đã huỷ' },
]

export default function ProjectsPage() {
  const [view, setView] = useState<ViewMode>('list')
  const [q, setQ] = useState('')
  const [searchParams] = useSearchParams()
  // Dashboard counters link here with ?status=IN_PROGRESS|ON_HOLD (docs/08 §2.2 "danh sách đã lọc")
  const [status, setStatus] = useState<string>(() => {
    const fromUrl = searchParams.get('status')
    return statusOptions.some(o => o.value === fromUrl) ? fromUrl! : ALL
  })
  const [createOpen, setCreateOpen] = useState(false)

  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (status !== ALL) params.set('status', status)
  params.set('pageSize', '100')

  const { data, isLoading } = useQuery({
    queryKey: ['projects', q, status],
    queryFn: () => api.get<PagedResult<ProjectListItem>>(`/admin/projects?${params}`),
  })

  const projects = data?.items ?? []
  const filtering = !!q || status !== ALL

  const viewButton = (mode: ViewMode, label: string, Icon: typeof List) => (
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
    <div className="space-y-6">
      <PageHeader
        title="Dự án"
        description="Theo dõi trạng thái, deadline và số deliverable."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus aria-hidden="true" />
            Tạo dự án
          </Button>
        }
      />

      <div className="rounded-xl border bg-card shadow-card">
        {/* toolbar */}
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center">
          <div className="relative w-full sm:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              aria-label="Tìm dự án"
              placeholder="Tìm dự án…"
              value={q}
              onChange={e => setQ(e.target.value)}
              className="pl-9"
            />
          </div>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="w-full sm:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {statusOptions.map(o => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="flex items-center gap-1 rounded-lg bg-muted p-1 sm:ml-auto" role="group" aria-label="Chế độ xem">
            {viewButton('list', 'Danh sách', List)}
            {viewButton('gantt', 'Timeline Gantt', GanttChartSquare)}
          </div>
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-11 w-full" />
            ))}
          </div>
        ) : view === 'list' ? (
          <ProjectsTable projects={projects} filtering={filtering} onCreate={() => setCreateOpen(true)} />
        ) : (
          <div className="p-4">
            <ProjectsGantt projects={projects} />
          </div>
        )}
      </div>

      <CreateProjectDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </div>
  )
}
