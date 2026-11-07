import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { MoreHorizontal, Package, Pencil, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { EmptyState } from '@/components/EmptyState'
import { QuotaMeter } from '@/components/QuotaMeter'
import { StatusBadge } from '@/components/StatusBadge'
import { api, type ApiError } from '@/lib/api'
import { formatDate } from '@/lib/format'
import type { Deliverable, DeliverableStatus, ProjectDetail } from '@/lib/types'
import { DeliverableDialog } from './DeliverableDialog'

// Mirrors Deliverable.ValidateStatusTransition in the backend domain
const transitions: Record<DeliverableStatus, { to: DeliverableStatus; label: string }[]> = {
  NOT_STARTED: [{ to: 'IN_PROGRESS', label: 'Bắt đầu làm' }, { to: 'CANCELLED', label: 'Huỷ' }],
  IN_PROGRESS: [{ to: 'IN_REVIEW', label: 'Gửi khách duyệt' }, { to: 'CANCELLED', label: 'Huỷ' }],
  IN_REVIEW: [
    { to: 'APPROVED', label: 'Khách đã duyệt' },
    { to: 'IN_PROGRESS', label: 'Quay lại làm' },
    { to: 'CANCELLED', label: 'Huỷ' },
  ],
  APPROVED: [{ to: 'DELIVERED', label: 'Đã bàn giao' }, { to: 'IN_PROGRESS', label: 'Quay lại làm' }],
  DELIVERED: [{ to: 'IN_PROGRESS', label: 'Mở lại' }],
  CANCELLED: [],
}

export function DeliverablesTab({ project }: { project: ProjectDetail }) {
  const qc = useQueryClient()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<Deliverable | null>(null)

  const statusMutation = useMutation({
    mutationFn: ({ id, to }: { id: string; to: DeliverableStatus }) =>
      api.post(`/admin/deliverables/${id}/status`, { to }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects', project.id] })
      toast.success('Đã cập nhật trạng thái')
    },
    onError: (err: ApiError) =>
      toast.error(
        err.code === 'INVALID_TRANSITION'
          ? 'Chuyển trạng thái không hợp lệ'
          : (err.detail ?? 'Không thể cập nhật trạng thái'),
      ),
  })

  function openCreate() {
    setEditing(null)
    setDialogOpen(true)
  }

  const items = project.deliverables.slice().sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <div className="rounded-xl border bg-card shadow-card">
      <div className="flex items-center justify-between gap-3 border-b p-4">
        <p className="text-sm text-muted-foreground">{items.length} deliverable</p>
        <Button size="sm" onClick={openCreate}>
          <Plus aria-hidden="true" />
          Thêm deliverable
        </Button>
      </div>

      {items.length === 0 ? (
        <EmptyState
          icon={Package}
          title="Chưa có deliverable"
          description="Thêm các hạng mục bàn giao để theo dõi số vòng sửa và lập báo giá."
          action={
            <Button onClick={openCreate}>
              <Plus aria-hidden="true" />
              Thêm deliverable
            </Button>
          }
        />
      ) : (
        <ul className="divide-y">
          {items.map(d => {
            const next = transitions[d.status] ?? []
            return (
              <li key={d.id} className="flex flex-col gap-3 px-4 py-3.5 sm:flex-row sm:items-center sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-foreground">{d.name}</p>
                  <p className="text-xs text-muted-foreground">Số lượng: {d.quantity}</p>
                </div>
                <div className="w-full sm:w-40">
                  <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="vòng" />
                </div>
                <div className="flex items-center justify-between gap-3 sm:justify-end">
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
                            disabled={statusMutation.isPending}
                          >
                            <MoreHorizontal aria-hidden="true" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          {next.map(t => (
                            <DropdownMenuItem
                              key={t.to}
                              className={t.to === 'CANCELLED' ? 'text-red-700 focus:text-red-700' : undefined}
                              onClick={() => statusMutation.mutate({ id: d.id, to: t.to })}
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

      <DeliverableDialog
        projectId={project.id}
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        editing={editing}
      />
    </div>
  )
}
