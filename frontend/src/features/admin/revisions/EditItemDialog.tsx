import { useState, type FormEvent } from 'react'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import type { ApiError } from '@/lib/api'
import { billingLabel, itemTypeLabel } from '@/lib/revisionLabels'
import type { Billing, RevisionItem, RevisionItemType } from '@/lib/revisionTypes'
import { errorText } from './revisionUtils'
import { useUpdateRevisionItem } from './useRevisions'

interface Props {
  item: RevisionItem
  projectId: string
  deliverableId: string | null
  open: boolean
  onClose: () => void
}

/** PUT /admin/revision-items/{id}; BR-61 — billing is locked while the line sits on a SENT/ACCEPTED quotation. */
export function EditItemDialog({ item, projectId, deliverableId, open, onClose }: Props) {
  const billingLocked = item.quotation?.status === 'SENT' || item.quotation?.status === 'ACCEPTED'
  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Sửa mục</DialogTitle>
          <DialogDescription>
            {billingLocked
              ? 'Mục đã nằm trong báo giá đã gửi — muốn đổi cách tính phí, hãy tạo version báo giá mới và bỏ dòng này trước.'
              : 'Đổi cách tính phí sẽ cập nhật số vòng đã dùng của hạng mục.'}
          </DialogDescription>
        </DialogHeader>
        <EditItemForm item={item} projectId={projectId} deliverableId={deliverableId} billingLocked={billingLocked} onClose={onClose} />
      </DialogContent>
    </Dialog>
  )
}

function EditItemForm({ item, projectId, deliverableId, billingLocked, onClose }: Omit<Props, 'open'> & { billingLocked: boolean }) {
  const [description, setDescription] = useState(item.description)
  const [type, setType] = useState<RevisionItemType>(item.type)
  const [billing, setBilling] = useState<Billing>(item.billing)
  const [internalNotes, setInternalNotes] = useState(item.internalNotes)
  const update = useUpdateRevisionItem(projectId)

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!description.trim()) { toast.error('Nhập mô tả'); return }
    update.mutate(
      { id: item.id, body: { description: description.trim(), type, billing, internalNotes, rowVersion: item.rowVersion } },
      {
        onSuccess: () => { toast.success('Đã cập nhật mục'); onClose() },
        onError: err => toast.error(errorText(err as ApiError, 'Không thể cập nhật mục')),
      },
    )
  }

  return (
    <form onSubmit={submit} className="space-y-4 py-2" noValidate>
      <div className="space-y-2">
        <Label htmlFor="edit-item-description">Mô tả</Label>
        <Input id="edit-item-description" maxLength={2000} value={description} onChange={e => setDescription(e.target.value)} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="edit-item-type">Loại</Label>
          <Select value={type} onValueChange={v => setType(v as RevisionItemType)}>
            <SelectTrigger id="edit-item-type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(Object.keys(itemTypeLabel) as RevisionItemType[]).map(t => <SelectItem key={t} value={t}>{itemTypeLabel[t]}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-item-billing">Tính phí</Label>
          <Select value={billing} onValueChange={v => setBilling(v as Billing)} disabled={billingLocked}>
            <SelectTrigger id="edit-item-billing">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(Object.keys(billingLabel) as Billing[]).filter(b => deliverableId !== null || b !== 'IN_QUOTA').map(b => <SelectItem key={b} value={b}>{billingLabel[b]}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="space-y-2">
        <Label htmlFor="edit-item-notes">Ghi chú nội bộ</Label>
        <Textarea id="edit-item-notes" rows={3} maxLength={5000} value={internalNotes} onChange={e => setInternalNotes(e.target.value)} />
      </div>
      <DialogFooter className="gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="submit" disabled={update.isPending}>{update.isPending ? 'Đang lưu…' : 'Lưu'}</Button>
      </DialogFooter>
    </form>
  )
}
