import type { ReactNode } from 'react'
import { Button, type ButtonProps } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

interface ConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: ReactNode
  confirmLabel: string
  onConfirm: () => void
  pending?: boolean
  destructive?: boolean
  cancelLabel?: string
  /** button size; the client portal passes `touch` (≥ 44px targets), admin keeps the default */
  size?: ButtonProps['size']
}

/** docs/08 §1: confirmation for irreversible actions (send quotation, delete draft…). */
export function ConfirmDialog({ open, onOpenChange, title, description, confirmLabel, onConfirm, pending, destructive, cancelLabel = 'Huỷ', size }: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" size={size} onClick={() => onOpenChange(false)}>{cancelLabel}</Button>
          <Button type="button" variant={destructive ? 'destructive' : 'default'} size={size} disabled={pending} onClick={onConfirm}>
            {pending ? 'Đang xử lý…' : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
