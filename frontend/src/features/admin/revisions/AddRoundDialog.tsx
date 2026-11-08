import { useState } from 'react'
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
import type { ApiError } from '@/lib/api'
import type { RoundInput } from '@/lib/revisionTypes'
import type { ProjectDetail } from '@/lib/types'
import { todayVn } from '@/features/admin/tasks/taskUtils'
import { RoundEditor } from './RoundEditor'
import { cleanRounds, emptyRound, errorText } from './revisionUtils'
import { useCreateRound } from './useRevisions'

interface Props {
  project: ProjectDetail
  open: boolean
  onClose: () => void
}

/** POST /admin/projects/{id}/rounds — a round that did not come through the inbox (docs/08 …/revisions). */
export function AddRoundDialog({ project, open, onClose }: Props) {
  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Thêm vòng sửa</DialogTitle>
          <DialogDescription>Ghi lại một vòng sửa không qua hộp thư yêu cầu. Cách tính phí được gợi ý theo quota.</DialogDescription>
        </DialogHeader>
        <AddRoundForm project={project} onClose={onClose} />
      </DialogContent>
    </Dialog>
  )
}

function AddRoundForm({ project, onClose }: Omit<Props, 'open'>) {
  const deliverables = project.deliverables.filter(d => d.status !== 'CANCELLED')
  const [round, setRound] = useState<RoundInput>(() => emptyRound(deliverables[0]?.id ?? null, todayVn()))
  const create = useCreateRound(project.id)

  function submit() {
    const [cleaned] = cleanRounds([round])
    if (!cleaned) { toast.error('Nhập ít nhất 1 mục sửa'); return }
    create.mutate(cleaned, {
      onSuccess: () => { toast.success('Đã thêm vòng sửa'); onClose() },
      onError: err => toast.error(errorText(err as ApiError, 'Không thể thêm vòng sửa')),
    })
  }

  return (
    <div className="space-y-4">
      <RoundEditor index={0} value={round} deliverables={deliverables} onChange={setRound} />
      <DialogFooter className="gap-2">
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="button" disabled={create.isPending} onClick={submit}>{create.isPending ? 'Đang lưu…' : 'Thêm vòng'}</Button>
      </DialogFooter>
    </div>
  )
}
