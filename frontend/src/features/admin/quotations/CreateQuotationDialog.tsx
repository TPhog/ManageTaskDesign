import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { StatusBadge } from '@/components/StatusBadge'
import type { ApiError } from '@/lib/api'
import type { ProjectDetail } from '@/lib/types'
import { isFinishedDeliverable, quotationErrorMessage } from './quotationUtils'
import { useCreateQuotation } from './useQuotations'

type Mode = 'all' | 'selected'

const optionClass = 'flex items-start gap-3 rounded-lg border p-3 has-checked:border-primary has-disabled:opacity-60'

export function CreateQuotationDialog({ project, open, onClose }: { project: ProjectDetail; open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const create = useCreateQuotation(project.id)
  const [mode, setMode] = useState<Mode>('all')
  const [selected, setSelected] = useState<string[]>([])
  const [title, setTitle] = useState('')

  const active = project.deliverables.filter(d => d.status !== 'CANCELLED')
  const finished = project.deliverables.filter(d => isFinishedDeliverable(d.status))
  const canSubmit = mode === 'all' || selected.length > 0

  function close() {
    setMode('all')
    setSelected([])
    setTitle('')
    onClose()
  }

  function toggle(id: string) {
    setSelected(s => (s.includes(id) ? s.filter(x => x !== id) : [...s, id]))
  }

  function submit() {
    create.mutate(
      {
        kind: 'INITIAL',
        title: title.trim() || null,
        fromDeliverables: true,
        deliverableIds: mode === 'selected' ? selected : undefined,
      },
      {
        onSuccess: res => {
          for (const w of res.warnings) toast.warning(w)
          toast.success('Đã tạo báo giá nháp')
          close()
          navigate(`/admin/quotations/${res.id}`)
        },
        onError: err => toast.error(quotationErrorMessage(err as ApiError, 'Không thể tạo báo giá')),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) close() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Tạo báo giá</DialogTitle>
          <DialogDescription>
            Mỗi deliverable thành một dòng: số lượng × giá gốc của dịch vụ. Bạn sửa được trước khi gửi.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-5 py-2">
          <fieldset className="space-y-2">
            <legend className="mb-1 text-sm font-medium text-foreground">Lập báo giá cho</legend>
            <label className={optionClass}>
              <input type="radio" name="quotation-mode" className="mt-1" checked={mode === 'all'} onChange={() => setMode('all')} />
              <span>
                <span className="block text-sm font-medium text-foreground">Toàn bộ dự án</span>
                <span className="block text-xs text-muted-foreground">{active.length} deliverable (trừ hạng mục đã huỷ)</span>
              </span>
            </label>
            <label className={optionClass}>
              <input
                type="radio"
                name="quotation-mode"
                className="mt-1"
                checked={mode === 'selected'}
                disabled={finished.length === 0}
                onChange={() => setMode('selected')}
              />
              <span>
                <span className="block text-sm font-medium text-foreground">Chọn hạng mục đã xong</span>
                <span className="block text-xs text-muted-foreground">
                  {finished.length === 0
                    ? 'Chưa có hạng mục nào ở trạng thái Khách đã duyệt / Đã bàn giao'
                    : 'Báo giá riêng với số và mã QR riêng'}
                </span>
              </span>
            </label>
          </fieldset>

          {mode === 'selected' && (
            <fieldset className="space-y-2">
              <legend className="mb-1 text-sm font-medium text-foreground">Hạng mục</legend>
              {finished.map(d => (
                <label key={d.id} className="flex items-center justify-between gap-3 rounded-lg border px-3 py-2">
                  <span className="flex items-center gap-3">
                    <input type="checkbox" checked={selected.includes(d.id)} onChange={() => toggle(d.id)} />
                    <span className="text-sm text-foreground">{d.name}</span>
                  </span>
                  <StatusBadge status={d.status} />
                </label>
              ))}
            </fieldset>
          )}

          <div className="space-y-2">
            <Label htmlFor="quotation-title">Tiêu đề báo giá</Label>
            <Input
              id="quotation-title"
              value={title}
              maxLength={200}
              placeholder="Để trống để dùng tiêu đề mặc định"
              onChange={e => setTitle(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>Huỷ</Button>
          <Button type="button" disabled={!canSubmit || create.isPending} onClick={submit}>
            {create.isPending ? 'Đang tạo…' : 'Tạo nháp'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
