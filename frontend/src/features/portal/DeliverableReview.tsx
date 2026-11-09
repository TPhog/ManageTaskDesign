import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { qk } from '@/lib/queryKeys'
import type { ProjectStatus } from '@/lib/types'
import type { PortalDeliverable } from '@/lib/portalTypes'
import { acceptsRequests } from './requests'
import { PortalRequestForm, type SentRequestSummary } from './PortalRequestForm'
import { RequestSentSummary } from './RequestSentSummary'
import { progressText, reviewState } from './review'
import { useApproveDeliverable } from './useDeliverableReview'

interface Props {
  projectId: string
  projectStatus: ProjectStatus
  deliverable: PortalDeliverable
}

/** Plan 06b §6 Portal: progress bar + "Duyệt" / "Yêu cầu sửa" on a deliverable under review. */
export function DeliverableReview({ projectId, projectStatus, deliverable: d }: Props) {
  const [approveOpen, setApproveOpen] = useState(false)
  const [changeOpen, setChangeOpen] = useState(false)
  const approve = useApproveDeliverable(projectId)
  const state = reviewState(d)
  const progress = progressText(d)

  return (
    <div className="mt-2 space-y-2">
      {progress && (
        <div className="flex items-center gap-2">
          <div
            role="progressbar"
            aria-label={`Tiến độ ${d.name}`}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={d.progressPercent ?? 0}
            className="h-1.5 w-32 overflow-hidden rounded-full bg-slate-100"
          >
            <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${d.progressPercent ?? 0}%` }} />
          </div>
          <span className="text-xs tabular-nums text-muted-foreground">{progress}</span>
        </div>
      )}

      {state === 'APPROVED_BY_CLIENT' && (
        <p className="text-xs font-medium text-teal-700">Bạn đã duyệt — đang chờ designer xác nhận</p>
      )}

      {state === 'AWAITING' && (
        <div className="flex flex-wrap gap-2">
          <Button size="sm" aria-label={`Duyệt ${d.name}`} onClick={() => setApproveOpen(true)}>Duyệt</Button>
          {acceptsRequests(projectStatus) && (
            <Button size="sm" variant="outline" aria-label={`Yêu cầu sửa ${d.name}`} onClick={() => setChangeOpen(true)}>
              Yêu cầu sửa
            </Button>
          )}
        </div>
      )}

      <ConfirmDialog
        open={approveOpen}
        onOpenChange={setApproveOpen}
        title={`Duyệt ${d.name}?`}
        description="Bạn xác nhận hạng mục này đã đạt yêu cầu. Designer sẽ xác nhận và chuẩn bị bàn giao."
        confirmLabel="Duyệt"
        pending={approve.isPending}
        onConfirm={() => approve.mutate(d, { onSettled: () => setApproveOpen(false) })}
      />
      <RequestChangeDialog projectId={projectId} deliverable={d} open={changeOpen} onOpenChange={setChangeOpen} />
    </div>
  )
}

interface RequestChangeProps {
  projectId: string
  deliverable: PortalDeliverable
  open: boolean
  onOpenChange: (open: boolean) => void
}

/** "Yêu cầu sửa": the shared PortalRequestForm fixed to this product, then a sent summary (CF-11/13). */
function RequestChangeDialog({ projectId, deliverable, open, onOpenChange }: RequestChangeProps) {
  const qc = useQueryClient()
  const [sent, setSent] = useState<SentRequestSummary | null>(null)

  function change(next: boolean) {
    if (!next) setSent(null)
    onOpenChange(next)
  }

  return (
    <Dialog open={open} onOpenChange={change}>
      <DialogContent className="max-h-[90dvh] max-w-md overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{`Yêu cầu sửa ${deliverable.name}`}</DialogTitle>
          <DialogDescription>Mô tả điều bạn muốn chỉnh — nhà thiết kế sẽ xem và phản hồi.</DialogDescription>
        </DialogHeader>
        {sent ? (
          <RequestSentSummary
            summary={sent}
            deliverableName={deliverable.name}
            actions={<Button type="button" size="touch" className="w-full" onClick={() => change(false)}>Xong</Button>}
          />
        ) : (
          <PortalRequestForm
            projects={[{ id: projectId, name: '' }]}
            defaultProjectId={projectId}
            fixedDeliverable={deliverable}
            onSent={summary => {
              setSent(summary)
              void qc.invalidateQueries({ queryKey: qk.portal.project(projectId) })
            }}
            onCancel={() => change(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
