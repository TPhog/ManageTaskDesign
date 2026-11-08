import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import type { ProjectStatus } from '@/lib/types'
import type { PortalDeliverable } from '@/lib/portalTypes'
import { REQUEST_MAX_LENGTH, acceptsRequests, requestErrorMessage } from './requests'
import { useSendPortalRequest } from './usePortal'
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

/** "Yêu cầu sửa": a client request tied to this deliverable (POST /api/portal/projects/{id}/requests, Plan 06). */
function RequestChangeDialog({ projectId, deliverable, open, onOpenChange }: RequestChangeProps) {
  const send = useSendPortalRequest()
  const [content, setContent] = useState('')
  const [error, setError] = useState<string | null>(null)
  const fieldId = `change-${deliverable.id}`

  function submit(e: FormEvent) {
    e.preventDefault()
    const text = content.trim()
    if (!text) {
      setError('Nhập nội dung cần sửa')
      return
    }
    send.mutate(
      { projectId, deliverableId: deliverable.id, content: text },
      {
        onSuccess: () => {
          toast.success('Đã gửi yêu cầu chỉnh sửa')
          setContent('')
          setError(null)
          onOpenChange(false)
        },
        onError: err => setError(requestErrorMessage(err)),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Yêu cầu sửa {deliverable.name}</DialogTitle>
          <DialogDescription>Mô tả điều bạn muốn chỉnh — designer sẽ xem và phản hồi.</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-3" noValidate>
          <div className="space-y-2">
            <Label htmlFor={fieldId}>Nội dung cần sửa</Label>
            <Textarea
              id={fieldId}
              rows={5}
              maxLength={REQUEST_MAX_LENGTH}
              value={content}
              onChange={e => setContent(e.target.value)}
              aria-invalid={!!error}
              aria-describedby={error ? `${fieldId}-error` : undefined}
            />
            {error && <p id={`${fieldId}-error`} className="text-sm text-destructive">{error}</p>}
          </div>
          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Huỷ</Button>
            <Button type="submit" disabled={send.isPending}>{send.isPending ? 'Đang gửi…' : 'Gửi yêu cầu'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
