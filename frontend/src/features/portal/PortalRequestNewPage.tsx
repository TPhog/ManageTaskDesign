import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowLeft, FolderOpen } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { Kicker } from './kit/editorial'
import { PortalRequestForm, type SentRequestSummary } from './PortalRequestForm'
import { RequestSentSummary } from './RequestSentSummary'
import { acceptsRequests } from './requests'
import { usePortalProjects } from './usePortal'

/** docs/08 §3.2 "/portal/requests/new" — text + up to 5 photos (plan 08), one shared form (Plan 12 CF-11/12/13). */
export default function PortalRequestNewPage() {
  const [params] = useSearchParams()
  const presetProjectId = params.get('projectId') ?? ''
  const { data: projects, isLoading } = usePortalProjects()
  const [sent, setSent] = useState<SentRequestSummary | null>(null)

  // BR-21/53: only open projects are offered; a preselected closed project is dropped
  const openProjects = (projects ?? []).filter(p => acceptsRequests(p.status))
  const defaultProjectId = openProjects.some(p => p.id === presetProjectId) ? presetProjectId : undefined
  const backTo = presetProjectId ? `/portal/projects/${presetProjectId}` : '/portal/requests'

  return (
    <div className="space-y-6">
      <Link to={backTo} className="-ml-1 inline-flex min-h-11 items-center gap-1 px-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại
      </Link>
      <header className="-mt-2">
        <Kicker tone="highlight">Yêu cầu</Kicker>
        <h1 className="mt-2 font-display text-[2rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">Gửi yêu cầu chỉnh sửa</h1>
        <p className="mt-1.5 font-serif text-base text-muted-foreground">Nhà thiết kế sẽ xem và báo lại cho bạn.</p>
      </header>

      {sent ? (
        <RequestSentSummary
          summary={sent}
          deliverableName={sent.request.deliverableName}
          actions={
            <>
              <Button asChild size="touch" className="w-full sm:w-auto">
                <Link to="/portal/requests">Xem yêu cầu của tôi</Link>
              </Button>
              <Button type="button" variant="outline" size="touch" className="w-full sm:w-auto" onClick={() => setSent(null)}>
                Gửi yêu cầu khác
              </Button>
            </>
          }
        />
      ) : isLoading ? (
        <Skeleton className="h-64 w-full" />
      ) : openProjects.length === 0 ? (
        <div className="border-t-[3px] border-rule">
          <EmptyState
            icon={FolderOpen}
            title="Chưa có dự án nhận yêu cầu"
            description="Dự án đã hoàn thành hoặc đã huỷ không nhận yêu cầu mới. Vui lòng liên hệ trực tiếp nhà thiết kế."
          />
        </div>
      ) : (
        <div className="border-t-[3px] border-rule pt-5">
          <PortalRequestForm projects={openProjects} defaultProjectId={defaultProjectId} withTemplates onSent={setSent} />
        </div>
      )}
    </div>
  )
}
