import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DateInput } from '@/components/DateInput'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { ProjectDetail } from '@/lib/types'
import type { QuotationDetail } from '@/lib/quotationTypes'
import { acceptOutcome, quotationErrorMessage, todayVn, type PaymentAction } from './quotationUtils'
import { useQuotationCommand } from './useQuotations'

interface DialogProps {
  quotation: QuotationDetail
  open: boolean
  onClose: () => void
}

const failed = (err: Error) => toast.error(quotationErrorMessage(err as ApiError, 'Không thể cập nhật báo giá'))

/** BR-36: designer records the client's agreement (ADR-004 — the client never clicks accept). */
export function AcceptDialog({ quotation, open, onClose }: DialogProps) {
  const command = useQuotationCommand(quotation.id)
  const [date, setDate] = useState(todayVn())
  const [note, setNote] = useState('')
  const pending = (quotation.newDeliverables ?? []).filter(n => !n.createdDeliverableId)
  // what accept does depends on the project status (quotation detail has none) — only needed with new deliverables to add
  const { data: project, isError: projectError } = useQuery({
    queryKey: qk.projects.detail(quotation.projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${quotation.projectId}`),
    enabled: open && pending.length > 0,
  })
  // the outcome is only known once the project status is; until then say nothing about what will be created
  const outcome = project ? acceptOutcome(project.status) : null

  function close() {
    setNote('')
    onClose()
  }

  function submit() {
    command.mutate(
      { action: 'accept', body: { acceptedAt: date, note: note.trim() || null, rowVersion: quotation.rowVersion } },
      {
        onSuccess: () => {
          toast.success(pending.length > 0 && outcome?.creates
            ? `Đã đánh dấu chấp nhận · thêm ${pending.length} hạng mục mới`
            : 'Đã đánh dấu chấp nhận')
          close()
        },
        onError: failed,
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) close() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Đánh dấu đã chấp nhận</DialogTitle>
          <DialogDescription>Ghi nhận khách đã đồng ý báo giá {quotation.number} (qua Zalo, email…).</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          {pending.length > 0 && (
            <div className="rounded-lg border border-highlight/30 bg-highlight-soft p-3 text-sm text-foreground">
              <p className="font-medium">
                {!outcome
                  ? `Có ${pending.length} hạng mục mới trong báo giá này:`
                  : outcome.creates
                    ? `Sẽ thêm ${pending.length} hạng mục mới vào dự án:`
                    : `${pending.length} hạng mục mới sẽ không được tạo:`}
              </p>
              <ul className="mt-1 list-disc pl-5">
                {pending.map(n => (
                  <li key={n.revisionItemId}>
                    {n.itemDescription || n.serviceName || n.lineDescription}
                    {n.serviceName ? '' : ' — không có dịch vụ mẫu (0 vòng sửa miễn phí)'}
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-xs">
                {outcome ? outcome.text : projectError ? 'Không xác định được trạng thái dự án.' : 'Đang kiểm tra trạng thái dự án…'}
              </p>
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="accept-date">Ngày chấp nhận</Label>
            <DateInput id="accept-date" value={date} onChange={setDate} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="accept-note">Ghi chú</Label>
            <Textarea id="accept-note" rows={3} value={note} placeholder="VD: Khách xác nhận qua Zalo lúc 14:30" onChange={e => setNote(e.target.value)} />
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>Huỷ</Button>
          <Button type="button" disabled={!date || command.isPending} onClick={submit}>Xác nhận</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** BR-37: rejection always carries a reason. */
export function RejectDialog({ quotation, open, onClose }: DialogProps) {
  const command = useQuotationCommand(quotation.id)
  const [date, setDate] = useState(todayVn())
  const [reason, setReason] = useState('')

  function close() {
    setReason('')
    onClose()
  }

  function submit() {
    command.mutate(
      { action: 'reject', body: { rejectedAt: date, reason: reason.trim(), rowVersion: quotation.rowVersion } },
      {
        onSuccess: () => {
          toast.success('Đã ghi nhận từ chối')
          close()
        },
        onError: failed,
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) close() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Từ chối báo giá</DialogTitle>
          <DialogDescription>Báo giá {quotation.number} sẽ chuyển sang Từ chối; có thể tạo version mới sau.</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label htmlFor="reject-date">Ngày từ chối</Label>
            <DateInput id="reject-date" value={date} onChange={setDate} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="reject-reason">Lý do từ chối</Label>
            <Textarea id="reject-reason" rows={3} value={reason} onChange={e => setReason(e.target.value)} />
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>Huỷ</Button>
          <Button type="button" variant="destructive" disabled={!reason.trim() || command.isPending} onClick={submit}>Từ chối</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** BR-70..72: a payment step; undo requires a reason. Open while action ≠ null. */
export function PaymentDialog({ quotation, action, onClose }: { quotation: QuotationDetail; action: PaymentAction | null; onClose: () => void }) {
  const command = useQuotationCommand(quotation.id)
  const [date, setDate] = useState(todayVn())
  const [reason, setReason] = useState('')

  function close() {
    setReason('')
    onClose()
  }

  function submit() {
    if (!action) return
    command.mutate(
      {
        action: 'payment-status',
        body: { to: action.to, date, reason: action.undo ? reason.trim() : null, rowVersion: quotation.rowVersion },
      },
      {
        onSuccess: () => {
          toast.success('Đã cập nhật thanh toán')
          close()
        },
        onError: failed,
      },
    )
  }

  return (
    <Dialog open={action !== null} onOpenChange={v => { if (!v) close() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{action?.label ?? ''}</DialogTitle>
          <DialogDescription>
            App không tự xác nhận tiền về — hãy kiểm tra trên app ngân hàng trước khi đánh dấu.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label htmlFor="payment-date">Ngày</Label>
            <DateInput id="payment-date" value={date} onChange={setDate} />
          </div>
          {action?.undo && (
            <div className="space-y-2">
              <Label htmlFor="payment-reason">Lý do hoàn tác</Label>
              <Textarea id="payment-reason" rows={3} value={reason} onChange={e => setReason(e.target.value)} />
            </div>
          )}
        </div>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>Huỷ</Button>
          <Button type="button" disabled={!date || (!!action?.undo && !reason.trim()) || command.isPending} onClick={submit}>Lưu</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
