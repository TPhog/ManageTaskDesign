import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Plus, List, GanttChartSquare, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { Pagination } from '@/components/Pagination'
import { SearchCombobox } from '@/components/SearchCombobox'
import { api } from '@/lib/api'
import { useClientOptions } from '@/lib/pickers'
import { qk } from '@/lib/queryKeys'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { cn } from '@/lib/utils'
import type { ClientDetail, PagedResult, ProjectListItem } from '@/lib/types'
import { ProjectsTable } from './ProjectsTable'
import { ProjectsGantt } from './ProjectsGantt'
import { CreateProjectDialog } from './CreateProjectDialog'

type ViewMode = 'list' | 'gantt'

// Radix Select reserves '' for "no value", so "all" uses a sentinel.
const ALL = '_all'
const PAGE_SIZE = 20
/** The timeline shows one page of the newest projects matching the filters */
const GANTT_SIZE = 100

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
  const [searchParams] = useSearchParams()
  const [view, setView] = useState<ViewMode>('list')
  const [qInput, setQInput] = useState(searchParams.get('q') ?? '')
  // ?status= / ?clientId= let other screens (dashboard, client page) link to a filtered list
  const [status, setStatus] = useState<string>(() => {
    const s = searchParams.get('status')
    return statusOptions.some(o => o.value === s) && s ? s : ALL
  })
  const [clientId, setClientId] = useState(searchParams.get('clientId') ?? '')
  const [clientLabel, setClientLabel] = useState('')
  const [clientSearch, setClientSearch] = useState('')
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)
  const q = useDebouncedValue(qInput.trim())
  const clients = useClientOptions(clientSearch, true, true)

  // A clientId from the URL has no label yet
  const { data: linkedClient } = useQuery({
    queryKey: qk.clients.detail(clientId),
    queryFn: () => api.get<ClientDetail>(`/admin/clients/${clientId}`),
    enabled: !!clientId && !clientLabel,
  })

  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (status !== ALL) params.set('status', status)
  if (clientId) params.set('clientId', clientId)
  params.set('page', String(view === 'list' ? page : 1))
  params.set('pageSize', String(view === 'list' ? PAGE_SIZE : GANTT_SIZE))

  const { data, isLoading } = useQuery({
    queryKey: ['projects', 'list', { q, status, clientId, page, view }],
    queryFn: () => api.get<PagedResult<ProjectListItem>>(`/admin/projects?${params}`),
    placeholderData: keepPreviousData,
  })

  const projects = data?.items ?? []
  const filtering = !!q || status !== ALL || !!clientId

  const viewButton = (mode: ViewMode, label: string, Icon: typeof List) => (
    <button
      type="button"
      onClick={() => setView(mode)}
      title={label}
      aria-label={label}
      aria-pressed={view === mode}
      className={cn(
        'inline-flex h-9 w-9 items-center justify-center rounded-md transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        view === mode ? 'bg-card text-foreground shadow-card' : 'text-muted-foreground hover:text-foreground',
      )}
    >
      <Icon className="h-4 w-4" aria-hidden="true" />
    </button>
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dự án"
        description="Theo dõi trạng thái, deadline và số hạng mục."
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus aria-hidden="true" />
            Tạo dự án
          </Button>
        }
      />

      <div className="rounded-xl border bg-card shadow-card">
        <div className="flex flex-col gap-3 border-b p-4 lg:flex-row lg:items-center">
          <div className="relative w-full lg:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              aria-label="Tìm dự án"
              placeholder="Tìm dự án hoặc tên khách…"
              value={qInput}
              onChange={e => { setQInput(e.target.value); setPage(1) }}
              className="pl-9"
            />
          </div>
          <Select value={status} onValueChange={v => { setStatus(v); setPage(1) }}>
            <SelectTrigger className="w-full lg:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {statusOptions.map(o => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <SearchCombobox
            id="projects-client-filter"
            ariaLabel="Lọc theo khách"
            className="lg:w-56"
            value={clientId}
            selectedLabel={clientLabel || linkedClient?.name}
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

          <div className="flex items-center gap-1 rounded-lg bg-muted p-1 lg:ml-auto" role="group" aria-label="Chế độ xem">
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
          <>
            <ProjectsTable projects={projects} filtering={filtering} onCreate={() => setCreateOpen(true)} />
            <Pagination page={page} pageSize={PAGE_SIZE} total={data?.total ?? 0} onPageChange={setPage} itemLabel="dự án" />
          </>
        ) : (
          <div className="space-y-2 p-4">
            {data && data.total > GANTT_SIZE && (
              <p className="text-xs text-muted-foreground">Timeline hiển thị {GANTT_SIZE} dự án mới nhất khớp bộ lọc — lọc thêm để thu hẹp.</p>
            )}
            <ProjectsGantt projects={projects} />
          </div>
        )}
      </div>

      <CreateProjectDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </div>
  )
}
