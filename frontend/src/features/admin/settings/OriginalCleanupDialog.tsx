import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatBytes } from '@/lib/images'
import type { OriginalCleanupPreview } from '@/lib/imageTypes'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  preview: OriginalCleanupPreview
  pending: boolean
  onConfirm: () => void
}

/** P10: irreversible — the designer must tick the acknowledgement before the destructive button unlocks. */
export function OriginalCleanupDialog({ open, onOpenChange, preview, pending, onConfirm }: Props) {
  const [understood, setUnderstood] = useState(false)
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{`Xoá bản gốc của ${preview.imageCount} ảnh?`}</DialogTitle>
          <DialogDescription>
            {`Giải phóng ${formatBytes(preview.freedBytes)} ở ${preview.projectCount} dự án đã hoàn thành. `}
            Bản gốc (đủ độ phân giải, không watermark) bị xoá vĩnh viễn; bản xem có watermark và ảnh thu nhỏ được giữ —
            khách vẫn xem như cũ. Không khôi phục được.
          </DialogDescription>
        </DialogHeader>
        <label className="flex min-h-11 items-center gap-2 text-sm text-foreground">
          <input type="checkbox" className="h-4 w-4 accent-primary" checked={understood}
            onChange={e => setUnderstood(e.target.checked)} />
          Tôi hiểu bản gốc sẽ bị xoá vĩnh viễn
        </label>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Huỷ</Button>
          <Button type="button" variant="destructive" disabled={!understood || pending} onClick={onConfirm}>
            {pending ? 'Đang xoá…' : 'Xoá vĩnh viễn bản gốc'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
