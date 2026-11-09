import { useState } from 'react'
import { Link } from 'react-router-dom'
import { MoreHorizontal } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { BillingChip } from '@/components/BillingChip'
import { ReasonDialog } from '@/components/ReasonDialog'
import { StatusBadge } from '@/components/StatusBadge'
import type { ApiError } from '@/lib/api'
import { itemTypeLabel } from '@/lib/revisionLabels'
import type { RevisionItem, WorkStatus } from '@/lib/revisionTypes'
import { EditItemDialog } from './EditItemDialog'
import { errorText, needsScopeOverride, workStatusTransitions } from './revisionUtils'
import { useChangeWorkStatus, useCreateItemTask } from './useRevisions'

interface Props {
  item: RevisionItem
  projectId: string
  /** null = project-level round (no quota, BR-41) */
  deliverableId: string | null
  selectable: boolean
  selected: boolean
  onToggle: () => void
}

export function RevisionItemRow({ item, projectId, deliverableId, selectable, selected, onToggle }: Props) {
  const [guardTo, setGuardTo] = useState<WorkStatus | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const workStatus = useChangeWorkStatus(projectId)
  const createTask = useCreateItemTask(projectId)

  function change(to: WorkStatus, overrideReason: string | null = null) {
    workStatus.mutate({ id: item.id, body: { to, overrideReason, rowVersion: item.rowVersion } }, {
      onSuccess: () => { setGuardTo(null); toast.success('Đã cập nhật trạng thái mục') },
      onError: err => {
        const apiErr = err as ApiError
        if (apiErr.code === 'SCOPE_GUARD') setGuardTo(to)   // 428 (docs/08 §4): ask for the reason
        else toast.error(errorText(apiErr, 'Không thể cập nhật trạng thái mục'))
      },
    })
  }

  function addTask() {
    createTask.mutate({ id: item.id, body: {} }, {
      onSuccess: () => toast.success('Đã tạo công việc'),
      onError: err => toast.error(errorText(err as ApiError, 'Không thể tạo công việc')),
    })
  }

  return (
    <li className="flex flex-col gap-2 rounded-lg bg-muted/40 px-3 py-2.5 sm:flex-row sm:items-center sm:gap-3">
      <div className="flex min-w-0 flex-1 items-start gap-2.5">
        {selectable ? (
          <input
            type="checkbox"
            className="mt-1 h-4 w-4 shrink-0 accent-primary"
            checked={selected}
            onChange={onToggle}
            aria-label={`Chọn ${item.description}`}
          />
        ) : (
          <span className="w-4 shrink-0" aria-hidden="true" />
        )}
        <div className="min-w-0">
          <p className="text-sm text-foreground">{item.description}</p>
          <p className="text-xs text-muted-foreground">
            {itemTypeLabel[item.type]}
            {item.scopeOverrideReason && <> · Làm trước báo giá: {item.scopeOverrideReason}</>}
            {item.tasks.length > 0 && (
              <>
                {' · '}
                <Link to={`/admin/projects/${projectId}?tab=tasks`} className="text-primary underline-offset-2 hover:underline">
                  {item.tasks.length} công việc
                </Link>
              </>
            )}
            {item.serviceName && !item.createdDeliverableId && <> · Dịch vụ: {item.serviceName}</>}
            {item.createdDeliverableId && (
              <>
                {' · '}
                <Link to={`/admin/projects/${projectId}?tab=deliverables`} className="text-primary underline-offset-2 hover:underline">
                  Đã thành hạng mục: {item.createdDeliverableName ?? 'xem Deliverables'}
                </Link>
              </>
            )}
          </p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-2 sm:justify-end">
        <BillingChip billing={item.billing} />
        {item.quotation && (
          <Link
            to={`/admin/quotations/${item.quotation.id}`}
            className="flex items-center gap-1.5 text-xs text-primary underline-offset-2 hover:underline"
          >
            {item.quotation.number ?? 'Báo giá nháp'} v{item.quotation.version}
            <StatusBadge status={item.quotation.effectiveStatus} />
          </Link>
        )}
        <StatusBadge status={item.workStatus} />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label={`Thao tác ${item.description}`} disabled={workStatus.isPending}>
              <MoreHorizontal aria-hidden="true" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {workStatusTransitions[item.workStatus].map(t => (
              <DropdownMenuItem
                key={t.to}
                className={t.to === 'CANCELLED' ? 'text-red-700 focus:text-red-700' : undefined}
                onClick={() => (needsScopeOverride(item, t.to) ? setGuardTo(t.to) : change(t.to))}
              >
                {t.label}
              </DropdownMenuItem>
            ))}
            <DropdownMenuItem onClick={() => setEditOpen(true)}>Sửa mục</DropdownMenuItem>
            <DropdownMenuItem disabled={createTask.isPending} onClick={addTask}>Tạo công việc</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
      <ReasonDialog
        open={guardTo !== null}
        onOpenChange={open => { if (!open) setGuardTo(null) }}
        title="Mục này tính phí nhưng báo giá chưa được chấp nhận"
        description="Bạn vẫn có thể làm trước — lý do được lưu vào lịch sử."
        label="Lý do vẫn làm"
        confirmLabel={guardTo === 'DONE' ? 'Vẫn hoàn thành' : 'Vẫn bắt đầu'}
        minLength={5}
        pending={workStatus.isPending}
        onConfirm={reason => { if (guardTo) change(guardTo, reason) }}
      />
      <EditItemDialog item={item} projectId={projectId} deliverableId={deliverableId} open={editOpen} onClose={() => setEditOpen(false)} />
    </li>
  )
}
