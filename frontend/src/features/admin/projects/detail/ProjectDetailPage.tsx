import { useParams, useSearchParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ChevronRight, History, SearchX } from 'lucide-react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { formatDate } from '@/lib/format'
import { projectDateRange, statusBadgeOverride } from '@/features/admin/flow/flowUtils'
import { api } from '@/lib/api'
import { BriefTab } from './BriefTab'
import { QuotationsTab } from './QuotationsTab'
import { DeliverablesTab } from './DeliverablesTab'
import { TasksTab } from './TasksTab'
import { RevisionsTab } from './RevisionsTab'
import { ProgressUpdateCard } from './ProgressUpdateCard'
import { FlowStepper } from './FlowStepper'
import { NextStepCard } from './NextStepCard'
import { ProjectStatusControl } from './ProjectStatusControl'
import { useProjectFlow } from '@/features/admin/flow/useFlow'
import type { ProjectDetail } from '@/lib/types'

const designTypeLabel: Record<string, string> = {
  LOGO: 'Logo', BANNER: 'Banner', POSTER: 'Poster', BRANDING: 'Thương hiệu',
  UI_UX: 'UI/UX', SOCIAL_MEDIA: 'Mạng xã hội', PACKAGING: 'Bao bì', PRINT: 'In ấn', OTHER: 'Khác',
}

/** QA D-17: 44px tall tabs on touch screens */
const tabTrigger = 'min-h-11 px-4 sm:min-h-0 sm:px-3'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

function InfoItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm font-medium text-foreground">{value}</dd>
    </div>
  )
}

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>()
  // ?tab=… deep-links into a tab (all-tasks page, NextStepCard links) and follows tab clicks
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') ?? 'overview'
  const flow = useProjectFlow(id)

  const { data: project, isLoading, error } = useQuery({
    queryKey: ['projects', id],
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${id}`),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-5 w-48" />
        <Skeleton className="h-28 w-full rounded-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }

  if (error || !project) {
    return (
      <div className={panelClass}>
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy dự án"
          description="Dự án có thể đã bị xoá hoặc đường dẫn không đúng."
          action={
            <Button asChild variant="outline">
              <Link to="/admin/projects">Về danh sách dự án</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const designType = designTypeLabel[project.designType] ?? project.designType
  const badge = statusBadgeOverride(project, flow.data)
  const range = projectDateRange(project.startDate, project.dueDate)

  return (
    <div className="space-y-6">
      {/* breadcrumb */}
      <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/admin/projects" className="rounded-sm hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          Dự án
        </Link>
        <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
        <span className="truncate font-medium text-foreground" aria-current="page">{project.name}</span>
      </nav>

      {/* header */}
      <div className={panelClass}>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="truncate text-2xl font-semibold tracking-tight text-foreground">{project.name}</h1>
              <StatusBadge status={badge.status} label={badge.label} />
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
              <span>{project.client.name}</span>
              <span aria-hidden="true">·</span>
              <span>{designType}</span>
              {range && (
                <>
                  <span aria-hidden="true">·</span>
                  <span className="tabular-nums">{range}</span>
                </>
              )}
            </div>
            {project.statusReason && (project.status === 'ON_HOLD' || project.status === 'CANCELLED' || project.status === 'IN_PROGRESS') && (
              <p className="mt-2 text-sm text-amber-800">Lý do: {project.statusReason}</p>
            )}
          </div>
          <ProjectStatusControl project={project} />
        </div>
      </div>

      {flow.data && <FlowStepper flow={flow.data} />}
      <NextStepCard project={project} flow={flow.data} isLoading={flow.isLoading} isError={flow.isError} isFetching={flow.isFetching} />

      {/* tabs */}
      <Tabs
        value={tab}
        onValueChange={value =>
          setSearchParams(prev => {
            const next = new URLSearchParams(prev)
            next.set('tab', value)
            return next
          }, { replace: true })
        }
      >
        {/* the fade hints that the tab bar scrolls on narrow screens */}
        <div className="relative">
          <TabsList className="h-auto w-full justify-start overflow-x-auto sm:w-auto">
            <TabsTrigger value="overview" className={tabTrigger}>Tổng quan</TabsTrigger>
            <TabsTrigger value="deliverables" className={tabTrigger}>Deliverables ({project.deliverables.length})</TabsTrigger>
            <TabsTrigger value="revisions" className={tabTrigger}>Yêu cầu & Revision</TabsTrigger>
            <TabsTrigger value="brief" className={tabTrigger}>Brief</TabsTrigger>
            <TabsTrigger value="tasks" className={tabTrigger}>Công việc</TabsTrigger>
            <TabsTrigger value="quotations" className={tabTrigger}>Báo giá</TabsTrigger>
            <TabsTrigger value="activity" className={tabTrigger}>Lịch sử</TabsTrigger>
          </TabsList>
          <div aria-hidden="true" className="pointer-events-none absolute inset-y-0 right-0 w-8 rounded-r-md bg-linear-to-l from-muted sm:hidden" />
        </div>

        <TabsContent value="overview" className="mt-4 space-y-4">
          <div className={`${panelClass} space-y-5`}>
            <dl className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
              <InfoItem label="Khách hàng" value={project.client.name} />
              <InfoItem label="Loại thiết kế" value={designType} />
              <InfoItem label="Ngày bắt đầu" value={formatDate(project.startDate)} />
              <InfoItem label="Deadline" value={formatDate(project.dueDate)} />
            </dl>
            {project.internalNotes && (
              <div>
                <p className="mb-1.5 text-xs font-medium text-muted-foreground">Ghi chú nội bộ</p>
                <p className="whitespace-pre-wrap rounded-lg bg-muted p-3 text-sm text-foreground">
                  {project.internalNotes}
                </p>
              </div>
            )}
          </div>
          <ProgressUpdateCard projectId={project.id} />
        </TabsContent>

        <TabsContent value="deliverables" className="mt-4">
          <DeliverablesTab project={project} />
        </TabsContent>

        <TabsContent value="revisions" className="mt-4">
          <RevisionsTab project={project} />
        </TabsContent>

        <TabsContent value="quotations" className="mt-4">
          <QuotationsTab project={project} />
        </TabsContent>

        <TabsContent value="brief" forceMount className="mt-4 data-[state=inactive]:hidden">
          <BriefTab
            key={`${project.brief?.id}-${project.brief?.updatedAt}`}
            projectId={project.id}
            brief={project.brief}
          />
        </TabsContent>

        <TabsContent value="tasks" className="mt-4">
          <TasksTab project={project} />
        </TabsContent>

        <TabsContent value="activity" className="mt-4">
          <div className={panelClass}>
            <EmptyState icon={History} title="Lịch sử hoạt động" description="Tính năng đang được phát triển." />
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}
