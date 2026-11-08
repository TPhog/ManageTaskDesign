import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertTriangle, Copy, Info, QrCode } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { cn } from '@/lib/utils'
import type { ProjectDetail } from '@/lib/types'
import type { FlowAction, FlowState } from '@/lib/flowTypes'
import { actionHref } from '@/features/admin/flow/flowUtils'
import {
  useCompleteProject,
  useConfirmApproval,
  useCopyQuotationText,
  useDeliverableStatus,
  useSubmitReview,
} from '@/features/admin/flow/useFlow'
import { StartProjectDialog } from './StartProjectDialog'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

const actionKey = (a: FlowAction) =>
  `${a.kind}-${a.target.deliverableId ?? a.target.quotationId ?? a.target.requestId ?? ''}`

interface Props {
  project: ProjectDetail
  flow: FlowState | undefined
  isLoading: boolean
  isError: boolean
}

/** Plan 06b F1: one primary next step, warnings (never blocking, F4) and secondary links. BR-01: every step is a click. */
export function NextStepCard({ project, flow, isLoading, isError }: Props) {
  const [startOpen, setStartOpen] = useState(false)
  const [completeOpen, setCompleteOpen] = useState(false)
  const submitReview = useSubmitReview()
  const confirmApproval = useConfirmApproval()
  const deliverableStatus = useDeliverableStatus()
  const complete = useCompleteProject(project.id)
  const copyText = useCopyQuotationText()
  const busy = submitReview.isPending || confirmApproval.isPending || deliverableStatus.isPending || complete.isPending

  if (isLoading) {
    return (
      <div className={panelClass}>
        <Skeleton className="h-5 w-40" />
        <Skeleton className="mt-3 h-10 w-56" />
      </div>
    )
  }
  if (isError || !flow) {
    return (
      <div className={panelClass}>
        <p className="text-sm text-muted-foreground">Không tải được bước tiếp theo.</p>
      </div>
    )
  }

  function run(action: FlowAction) {
    const deliverableId = action.target.deliverableId
    switch (action.kind) {
      case 'START_PROJECT':
        setStartOpen(true)
        break
      case 'SUBMIT_REVIEW':
        if (deliverableId) submitReview.mutate({ deliverableId })
        break
      case 'CONFIRM_APPROVAL':
        if (deliverableId) confirmApproval.mutate(deliverableId)
        break
      case 'DELIVER':
        if (deliverableId) deliverableStatus.mutate({ deliverableId, to: 'DELIVERED' })
        break
      case 'COMPLETE_PROJECT':
        setCompleteOpen(true)
        break
      case 'COPY_QUOTATION_MESSAGE':
      case 'COPY_PAYMENT_REMINDER':
        if (action.target.quotationId) void copyText(action.kind, action.target.quotationId)
        break
      default:
        break
    }
  }

  function renderAction(action: FlowAction) {
    const variant = action.primary ? 'default' : 'outline'
    const size = action.primary ? 'default' : 'sm'
    const href = actionHref(action.kind, project.id, action.target)
    if (href) {
      return (
        <Button key={actionKey(action)} asChild variant={variant} size={size}>
          <Link to={href}>{action.label}</Link>
        </Button>
      )
    }
    const isCopy = action.kind === 'COPY_QUOTATION_MESSAGE' || action.kind === 'COPY_PAYMENT_REMINDER'
    return (
      <span key={actionKey(action)} className="inline-flex items-center gap-1">
        <Button type="button" variant={variant} size={size} disabled={busy} onClick={() => run(action)}>
          {isCopy && <Copy aria-hidden="true" />}
          {action.label}
        </Button>
        {isCopy && action.target.quotationId && (
          <Button asChild variant="ghost" size="sm">
            <Link to={`/admin/quotations/${action.target.quotationId}`}>
              <QrCode aria-hidden="true" />
              Xem QR
            </Link>
          </Button>
        )}
      </span>
    )
  }

  const [primary, ...others] = flow.actions
  const completeWarning = flow.warnings.map(w => w.message).join(' · ')

  return (
    <section aria-labelledby="next-step-heading" className={panelClass}>
      <h2 id="next-step-heading" className="text-sm font-semibold text-foreground">Bước tiếp theo</h2>

      {flow.warnings.length > 0 && (
        <ul aria-label="Cảnh báo" className="mt-3 space-y-2">
          {flow.warnings.map((w, i) => (
            <li
              key={`${w.kind}-${i}`}
              className={cn(
                'flex items-start gap-2 rounded-lg px-3 py-2 text-sm',
                w.severity === 'warning' ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-700',
              )}
            >
              {w.severity === 'warning'
                ? <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                : <Info className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />}
              {w.message}
            </li>
          ))}
        </ul>
      )}

      {flow.stage === 'CANCELLED' ? (
        <p className="mt-3 text-sm text-muted-foreground">Dự án đã huỷ — không có bước tiếp theo.</p>
      ) : !primary ? (
        <p className="mt-3 text-sm text-muted-foreground">
          {project.status === 'COMPLETED' ? 'Dự án đã hoàn tất.' : 'Chưa có việc cần làm ngay — đang chờ khách phản hồi.'}
        </p>
      ) : (
        <>
          <div className="mt-4">{renderAction(primary)}</div>
          {others.length > 0 && <div className="mt-3 flex flex-wrap items-center gap-2">{others.map(renderAction)}</div>}
        </>
      )}

      <StartProjectDialog project={project} open={startOpen} onOpenChange={setStartOpen} warnings={flow.warnings} />
      <ConfirmDialog
        open={completeOpen}
        onOpenChange={setCompleteOpen}
        title="Hoàn thành dự án?"
        description={
          completeWarning
            ? `Lưu ý: ${completeWarning}. Hệ thống chỉ cảnh báo, không chặn.`
            : 'Dự án sẽ chuyển sang Hoàn thành.'
        }
        confirmLabel="Hoàn thành"
        pending={complete.isPending}
        onConfirm={() => complete.mutate({ rowVersion: project.rowVersion }, { onSuccess: () => setCompleteOpen(false) })}
      />
    </section>
  )
}
