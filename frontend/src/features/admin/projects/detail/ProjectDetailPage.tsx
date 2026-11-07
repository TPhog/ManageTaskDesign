import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ChevronDown } from 'lucide-react'
import { toast } from 'sonner'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { StatusBadge } from '@/components/StatusBadge'
import { QuotaMeter } from '@/components/QuotaMeter'
import { formatDate } from '@/lib/format'
import { api } from '@/lib/api'
import type { ProjectDetail, ProjectStatus, Deliverable } from '@/lib/types'

const designTypeLabel: Record<string, string> = {
  LOGO: 'Logo', BANNER: 'Banner', POSTER: 'Poster', BRANDING: 'Thương hiệu',
  UI_UX: 'UI/UX', SOCIAL_MEDIA: 'Mạng xã hội', PACKAGING: 'Bao bì', PRINT: 'In ấn', OTHER: 'Khác',
}

const statusTransitions: Record<ProjectStatus, { to: ProjectStatus; label: string }[]> = {
  DRAFT: [
    { to: 'QUOTED', label: 'Chuyển sang Đã báo giá' },
    { to: 'IN_PROGRESS', label: 'Bắt đầu làm' },
    { to: 'CANCELLED', label: 'Huỷ dự án' },
  ],
  QUOTED: [
    { to: 'IN_PROGRESS', label: 'Bắt đầu làm' },
    { to: 'CANCELLED', label: 'Huỷ dự án' },
  ],
  IN_PROGRESS: [
    { to: 'ON_HOLD', label: 'Tạm dừng' },
    { to: 'COMPLETED', label: 'Hoàn thành' },
    { to: 'CANCELLED', label: 'Huỷ dự án' },
  ],
  ON_HOLD: [
    { to: 'IN_PROGRESS', label: 'Tiếp tục làm' },
    { to: 'CANCELLED', label: 'Huỷ dự án' },
  ],
  COMPLETED: [],
  CANCELLED: [{ to: 'DRAFT', label: 'Mở lại' }],
}

function DeliverableRow({ d }: { d: Deliverable }) {
  return (
    <div className="flex items-center gap-3 py-2.5 border-b border-gray-50 last:border-0">
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-gray-900 truncate">{d.name}</p>
        <p className="text-xs text-gray-400">SL: {d.quantity}</p>
      </div>
      <div className="w-36">
        <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="vòng" />
      </div>
      <StatusBadge status={d.status} />
      <span className="text-xs text-gray-400 w-20 text-right">{formatDate(d.dueDate)}</span>
    </div>
  )
}

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const { data: project, isLoading, error } = useQuery({
    queryKey: ['projects', id],
    queryFn: () => api.get<ProjectDetail>(`/api/admin/projects/${id}`),
    enabled: !!id,
  })

  const statusMutation = useMutation({
    mutationFn: (body: { to: ProjectStatus; rowVersion: number }) =>
      api.post(`/api/admin/projects/${id}/status`, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects'] })
      toast.success('Đã cập nhật trạng thái')
    },
    onError: (err: any) => {
      const code = err?.response?.data?.code
      if (code === 'INVALID_TRANSITION') toast.error('Chuyển trạng thái không hợp lệ')
      else if (code === 'CONCURRENCY_CONFLICT') toast.error('Dữ liệu đã thay đổi, vui lòng tải lại')
      else toast.error('Không thể cập nhật trạng thái')
    },
  })

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (error || !project) {
    return (
      <div className="text-center py-16 text-gray-400">
        <p>Không tìm thấy dự án</p>
        <Button variant="ghost" size="sm" className="mt-2" onClick={() => navigate('/admin/projects')}>
          Quay lại
        </Button>
      </div>
    )
  }

  const transitions = statusTransitions[project.status] ?? []

  return (
    <div className="space-y-4">
      {/* breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link to="/admin/projects" className="flex items-center gap-1 hover:text-gray-900">
          <ArrowLeft className="h-3.5 w-3.5" />
          Dự án
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{project.name}</span>
      </div>

      {/* header */}
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="text-lg font-semibold text-gray-900 truncate">{project.name}</h1>
            <div className="flex items-center gap-3 mt-1 text-sm text-gray-500">
              <span>{project.client.name}</span>
              <span>·</span>
              <span>{designTypeLabel[project.designType] ?? project.designType}</span>
              {project.startDate && (
                <>
                  <span>·</span>
                  <span>{formatDate(project.startDate)} → {formatDate(project.dueDate)}</span>
                </>
              )}
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <StatusBadge status={project.status} />
            {transitions.length > 0 && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="sm" disabled={statusMutation.isPending}>
                    Đổi trạng thái
                    <ChevronDown className="h-3.5 w-3.5 ml-1" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  {transitions.map(t => (
                    <DropdownMenuItem
                      key={t.to}
                      onClick={() => statusMutation.mutate({ to: t.to, rowVersion: project.rowVersion })}
                    >
                      {t.label}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </div>
      </div>

      {/* tabs */}
      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Tổng quan</TabsTrigger>
          <TabsTrigger value="deliverables">Deliverables ({project.deliverables.length})</TabsTrigger>
          <TabsTrigger value="brief">Brief</TabsTrigger>
          <TabsTrigger value="activity">Lịch sử</TabsTrigger>
        </TabsList>

        {/* overview tab */}
        <TabsContent value="overview">
          <div className="bg-white rounded-lg border border-gray-200 p-4 mt-2 space-y-4">
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <p className="text-gray-500 text-xs mb-0.5">Khách hàng</p>
                <p className="font-medium">{project.client.name}</p>
              </div>
              <div>
                <p className="text-gray-500 text-xs mb-0.5">Loại thiết kế</p>
                <p className="font-medium">{designTypeLabel[project.designType] ?? project.designType}</p>
              </div>
              <div>
                <p className="text-gray-500 text-xs mb-0.5">Ngày bắt đầu</p>
                <p className="font-medium">{formatDate(project.startDate)}</p>
              </div>
              <div>
                <p className="text-gray-500 text-xs mb-0.5">Deadline</p>
                <p className="font-medium">{formatDate(project.dueDate)}</p>
              </div>
            </div>
            {project.internalNotes && (
              <div>
                <p className="text-gray-500 text-xs mb-1">Ghi chú nội bộ</p>
                <p className="text-sm text-gray-700 bg-gray-50 rounded p-3 whitespace-pre-wrap">
                  {project.internalNotes}
                </p>
              </div>
            )}
          </div>
        </TabsContent>

        {/* deliverables tab */}
        <TabsContent value="deliverables">
          <div className="bg-white rounded-lg border border-gray-200 p-4 mt-2">
            {project.deliverables.length === 0 ? (
              <p className="text-center text-sm text-gray-400 py-8">Chưa có deliverable</p>
            ) : (
              <div>
                {project.deliverables
                  .slice()
                  .sort((a, b) => a.sortOrder - b.sortOrder)
                  .map(d => <DeliverableRow key={d.id} d={d} />)}
              </div>
            )}
          </div>
        </TabsContent>

        {/* brief tab */}
        <TabsContent value="brief">
          <div className="bg-white rounded-lg border border-gray-200 p-4 mt-2">
            {!project.brief ? (
              <p className="text-center text-sm text-gray-400 py-8">Chưa có brief</p>
            ) : (
              <div className="space-y-3">
                <div className="flex items-center gap-2 text-xs text-gray-400">
                  <span>Version {project.brief.version}</span>
                  <span>·</span>
                  <StatusBadge status={project.brief.status} />
                  {project.brief.confirmedAt && (
                    <>
                      <span>·</span>
                      <span>Confirmed {formatDate(project.brief.confirmedAt)}</span>
                    </>
                  )}
                </div>
                {project.brief.content ? (
                  <div className="prose prose-sm max-w-none text-gray-700 whitespace-pre-wrap text-sm">
                    {project.brief.content}
                  </div>
                ) : (
                  <p className="text-sm text-gray-400 italic">Brief chưa có nội dung</p>
                )}
              </div>
            )}
          </div>
        </TabsContent>

        {/* activity tab (placeholder) */}
        <TabsContent value="activity">
          <div className="bg-white rounded-lg border border-gray-200 p-4 mt-2">
            <p className="text-center text-sm text-gray-400 py-8">Lịch sử hoạt động — đang phát triển</p>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}
