import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus, List, GanttChartSquare } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { api } from '@/lib/api'
import type { PagedResult, ProjectListItem } from '@/lib/types'
import { ProjectsTable } from './ProjectsTable'
import { ProjectsGantt } from './ProjectsGantt'
import { CreateProjectDialog } from './CreateProjectDialog'

type ViewMode = 'list' | 'gantt'

const statusOptions: { value: string; label: string }[] = [
  { value: '', label: 'Tất cả trạng thái' },
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
  const [status, setStatus] = useState<string>('')
  const [createOpen, setCreateOpen] = useState(false)

  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (status) params.set('status', status)
  params.set('pageSize', '100')

  const { data, isLoading } = useQuery({
    queryKey: ['projects', q, status],
    queryFn: () => api.get<PagedResult<ProjectListItem>>(`/api/admin/projects?${params}`),
  })

  const projects = data?.items ?? []

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-gray-900">Dự án</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4 mr-1.5" />
          Tạo dự án
        </Button>
      </div>

      {/* toolbar */}
      <div className="flex items-center gap-3 flex-wrap">
        <Input
          placeholder="Tìm dự án…"
          value={q}
          onChange={e => setQ(e.target.value)}
          className="max-w-xs"
        />
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {statusOptions.map(o => (
              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="ml-auto flex items-center gap-1 border border-gray-200 rounded-md p-0.5">
          <Button
            variant={view === 'list' ? 'secondary' : 'ghost'}
            size="icon"
            className="h-7 w-7"
            onClick={() => setView('list')}
            title="Danh sách"
          >
            <List className="h-3.5 w-3.5" />
          </Button>
          <Button
            variant={view === 'gantt' ? 'secondary' : 'ghost'}
            size="icon"
            className="h-7 w-7"
            onClick={() => setView('gantt')}
            title="Timeline Gantt"
          >
            <GanttChartSquare className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

      {/* content */}
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : view === 'list' ? (
          <ProjectsTable projects={projects} />
        ) : (
          <ProjectsGantt projects={projects} />
        )}
      </div>

      <CreateProjectDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </div>
  )
}
