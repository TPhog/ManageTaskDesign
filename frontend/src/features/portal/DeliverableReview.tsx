import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Check, Pencil } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { qk } from '@/lib/queryKeys'
import type { ProjectStatus } from '@/lib/types'
import type { PortalDeliverable } from '@/lib/portalTypes'
import { acceptsRequests } from './requests'
import { DeliverableImages } from './DeliverableImages'
import { PortalRequestForm, type SentRequestSummary } from './PortalRequestForm'
import { RequestSentSummary } from './RequestSentSummary'
import { progressText, reviewState } from './review'
import { useApproveDeliverable } from './useDeliverableReview'

interface Props {
  projectId: string
  projectStatus: ProjectStatus
  deliverable: PortalDeliverable
}

/** Plan 06b §6 + Plan 12 CF-01/02/07: task progress, the images to approve, then "Duyệt" / "Yêu cầu sửa" — one voice per state. */
export function DeliverableReview({ projectId, projectStatus, deliverable: d }: Props) {
  const [approveOpen, setApproveOpen] = useState(false)
  const [changeOpen, setChangeOpen] = useState(false)
  const approve = useApproveDeliverable(projectId)
  const state = reviewState(d)
  const progress = progressText(d)

  return (
    <div className="mt-2 space-y-3">
      {progress && (
        <div className="flex items-center gap-3">
          <div
            role="progressbar"
            aria-label={`Tiến độ ${d.name}`}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={d.progressPercent ?? 0}
            className="h-0.5 w-32 overflow-hidden bg-border"
          >
            <div className="h-full bg-foreground transition-all motion-reduce:transition-none" style={{ width: `${d.progressPercent ?? 0}%` }} />
          </div>
          <span className="font-serif text-xs italic tabular-nums text-muted-foreground">{progress}</span>
        </div>
      )}

      {state === 'APPROVED_BY_CLIENT' && (
        <p className="font-serif text-sm font-medium italic text-success">
          Bạn đã duyệt — đang chờ nhà thiết kế xác nhận. Nếu đổi ý, hãy nhắn nhà thiết kế.
        </p>
      )}

      {state === 'CHANGES_IN_PROGRESS' && (
        <p className="border-l-4 border-foreground py-1 pl-4 font-serif text-sm italic leading-relaxed text-foreground">
          Nhà thiết kế đang sửa theo yêu cầu của bạn. Khi có bản mới, bạn sẽ duyệt lại ở đây.
        </p>
      )}

      {state === 'AWAITING' && (
        <>
          <DeliverableImages projectId={projectId} deliverableId={d.id} deliverableName={d.name} />
          <div className="grid grid-cols-2 gap-2.5">
            <Button className="h-11 w-full" aria-label={`Duyệt ${d.name}`} onClick={() => setApproveOpen(true)}>
              <Check aria-hidden="true" />
              Duyệt
            </Button>
            {acceptsRequests(projectStatus) && (
              <Button variant="outline" className="h-11 w-full" aria-label={`Yêu cầu sửa ${d.name}`} onClick={() => setChangeOpen(true)}>
                <Pencil aria-hidden="true" />
                Yêu cầu sửa
              </Button>
            )}
          </div>
        </>
      )}

      <ConfirmDialog
        open={approveOpen}
        onOpenChange={setApproveOpen}
        title={`Duyệt ${d.name}?`}
        description={`Bạn xác nhận ${d.name} đã đạt yêu cầu. Nhà thiết kế sẽ xác nhận và chuẩn bị bàn giao. Sau khi duyệt bạn không tự huỷ được — nếu đổi ý, hãy nhắn nhà thiết kế.`}
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
          <DialogDescription>
            {sent ? 'Yêu cầu đã đến nhà thiết kế.' : 'Mô tả điều bạn muốn chỉnh — nhà thiết kế sẽ xem và phản hồi.'}
          </DialogDescription>
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
