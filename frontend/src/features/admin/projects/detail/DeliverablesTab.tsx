import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckCircle2, MoreHorizontal, Package, Pencil, Plus, Send, Truck } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { QuotaMeter } from '@/components/QuotaMeter'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import { quoteRefLabel } from '@/lib/quoteRefs'
import type { Deliverable, DeliverableStatus, ProjectDetail } from '@/lib/types'
import { useDeliverableStatus } from '@/features/admin/flow/useFlow'
import { useDeliverableDialogs } from '@/features/admin/flow/useDeliverableDialogs'
import { DeliverableDialog } from './DeliverableDialog'

// Mirrors Deliverable.ValidateStatusTransition in the backend domain
const transitions: Record<DeliverableStatus, { to: DeliverableStatus; label: string }[]> = {
  NOT_STARTED: [{ to: 'IN_PROGRESS', label: 'Bắt đầu làm' }, { to: 'CANCELLED', label: 'Huỷ' }],
  IN_PROGRESS: [{ to: 'IN_REVIEW', label: 'Gửi khách duyệt' }, { to: 'CANCELLED', label: 'Huỷ' }],
  IN_REVIEW: [
    { to: 'APPROVED', label: 'Ghi nhận khách đã duyệt (hộ khách)' },
    { to: 'IN_PROGRESS', label: 'Quay lại làm' },
    { to: 'CANCELLED', label: 'Huỷ' },
  ],
  APPROVED: [{ to: 'DELIVERED', label: 'Đã bàn giao' }, { to: 'IN_PROGRESS', label: 'Quay lại làm' }],
  DELIVERED: [{ to: 'IN_PROGRESS', label: 'Mở lại' }],
  CANCELLED: [],
}

export function DeliverablesTab({ project }: { project: ProjectDetail }) {
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<Deliverable | null>(null)
  const statusMutation = useDeliverableStatus()
  const confirmDialogs = useDeliverableDialogs(project)
  const busy = statusMutation.isPending || confirmDialogs.pending
  const [searchParams, setSearchParams] = useSearchParams()
  const wantsNew = searchParams.get('new') === '1'

  /** IN_REVIEW / APPROVED / DELIVERED ask first (dialogs); the rest change at once. */
  function changeStatus(d: Deliverable, to: DeliverableStatus) {
    if (to === 'IN_REVIEW') confirmDialogs.open('submit', d.id)
    else if (to === 'APPROVED') confirmDialogs.open('approve', d.id)
    else if (to === 'DELIVERED') confirmDialogs.open('deliver', d.id)
    else statusMutation.mutate({ deliverableId: d.id, to })
  }

  function openCreate() {
    setEditing(null)
    setDialogOpen(true)
  }

  // Next-step "Thêm hạng mục" links here as ?new=1: open the dialog once, then drop the param (like ?step= on NextStepCard)
  useEffect(() => {
    if (!wantsNew) return
    setEditing(null)
    setDialogOpen(true)
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      next.delete('new')
      return next
    }, { replace: true })
  }, [wantsNew, setSearchParams])

  const items = project.deliverables.slice().sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <div className="rounded-xl border bg-card shadow-card">
      <div className="flex items-center justify-between gap-3 border-b p-4">
        <p className="text-sm text-muted-foreground">{items.length} hạng mục</p>
        <Button size="sm" onClick={openCreate}>
          <Plus aria-hidden="true" />
          Thêm hạng mục
        </Button>
      </div>

      {items.length === 0 ? (
        <EmptyState
          icon={Package}
          title="Chưa có hạng mục"
          description="Thêm các hạng mục bàn giao để theo dõi số vòng sửa và lập báo giá."
          action={
            <Button onClick={openCreate}>
              <Plus aria-hidden="true" />
              Thêm hạng mục
            </Button>
          }
        />
      ) : (
        <ul className="divide-y">
          {items.map(d => {
            const next = transitions[d.status] ?? []
            const approvedOnPortal = d.status === 'IN_REVIEW' && d.clientDecision === 'APPROVED'
            return (
              <li key={d.id} className="flex flex-col gap-3 px-4 py-3.5 sm:flex-row sm:items-center sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-foreground">{d.name}</p>
                  <p className="text-xs text-muted-foreground">
                    Số lượng: {d.quantity} · Vòng thêm: <MoneyText amount={d.extraRoundPrice} />
                  </p>
                  {(d.quotedIn ?? []).length > 0 && (
                    <p className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                      <span>Đã có trong:</span>
                      {(d.quotedIn ?? []).map(r => (
                        <Link
                          key={r.quotationId}
                          to={`/admin/quotations/${r.quotationId}`}
                          className="rounded-full bg-secondary px-2 py-0.5 font-medium text-secondary-foreground hover:underline"
                        >
                          {quoteRefLabel(r)}
                        </Link>
                      ))}
                    </p>
                  )}
                  {d.progressPercent !== null && d.progressPercent !== undefined && (
                    <div className="mt-1.5 flex items-center gap-2">
                      <div
                        role="progressbar"
                        aria-label={`Tiến độ công việc ${d.name}`}
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={d.progressPercent}
                        className="h-1.5 w-28 overflow-hidden rounded-full bg-muted"
                      >
                        <div className="h-full rounded-full bg-primary transition-[width] duration-300" style={{ width: `${d.progressPercent}%` }} />
                      </div>
                      <span className="text-xs tabular-nums text-muted-foreground">
                        {d.taskDone}/{d.taskTotal} việc · {d.progressPercent}%
                      </span>
                    </div>
                  )}
                  {approvedOnPortal && (
                    <div className="mt-1.5 flex flex-wrap items-center gap-2">
                      <span className="inline-flex items-center gap-1 rounded-full bg-teal-50 px-2 py-0.5 text-xs font-medium text-teal-700 ring-1 ring-inset ring-teal-200">
                        <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                        Khách đã duyệt trên portal
                      </span>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7"
                        aria-label={`Xác nhận duyệt ${d.name}`}
                        disabled={busy}
                        onClick={() => confirmDialogs.open('approve', d.id)}
                      >
                        Xác nhận
                      </Button>
                    </div>
                  )}
                </div>
                <div className="w-full sm:w-40">
                  <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="vòng" />
                </div>
                <div className="flex items-center justify-between gap-3 sm:justify-end">
                  {d.status === 'IN_PROGRESS' && (
                    <Button
                      size="sm"
                      variant="outline"
                      aria-label={`Gửi duyệt ${d.name}`}
                      disabled={busy}
                      onClick={() => confirmDialogs.open('submit', d.id)}
                    >
                      <Send aria-hidden="true" />
                      Gửi duyệt
                    </Button>
                  )}
                  {d.status === 'APPROVED' && (
                    <Button
                      size="sm"
                      variant="outline"
                      aria-label={`Bàn giao ${d.name}`}
                      disabled={busy}
                      onClick={() => confirmDialogs.open('deliver', d.id)}
                    >
                      <Truck aria-hidden="true" />
                      Bàn giao
                    </Button>
                  )}
                  <StatusBadge status={d.status} />
                  <span className="w-24 text-right text-xs tabular-nums text-muted-foreground">{formatDate(d.dueDate)}</span>
                  <div className="flex items-center">
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Sửa ${d.name}`}
                      onClick={() => { setEditing(d); setDialogOpen(true) }}
                    >
                      <Pencil aria-hidden="true" />
                    </Button>
                    {next.length > 0 && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Đổi trạng thái ${d.name}`}
                            disabled={busy}
                          >
                            <MoreHorizontal aria-hidden="true" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          {next.map(t => (
                            <DropdownMenuItem
                              key={t.to}
                              className={t.to === 'CANCELLED' ? 'text-red-700 focus:text-red-700' : undefined}
                              onClick={() => changeStatus(d, t.to)}
                            >
                              {t.label}
                            </DropdownMenuItem>
                          ))}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </div>
                </div>
              </li>
            )
          })}
        </ul>
      )}

      {confirmDialogs.dialogs}
      <DeliverableDialog
        projectId={project.id}
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        editing={editing}
      />
    </div>
  )
}
