import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface ReasonDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  label: string
  confirmLabel: string
  minLength?: number
  pending?: boolean
  destructive?: boolean
  onConfirm: (reason: string) => void
}

/** docs/08 §1 ReasonDialog: a required reason (scope override, decline…). The text resets every time it opens. */
export function ReasonDialog(props: ReasonDialogProps) {
  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{props.title}</DialogTitle>
          <DialogDescription>{props.description}</DialogDescription>
        </DialogHeader>
        <ReasonForm {...props} />
      </DialogContent>
    </Dialog>
  )
}

function ReasonForm({ onOpenChange, label, confirmLabel, minLength = 1, pending, destructive, onConfirm }: ReasonDialogProps) {
  const [reason, setReason] = useState('')
  const tooShort = reason.trim().length < minLength

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!tooShort) onConfirm(reason.trim())
  }

  return (
    <form onSubmit={submit} className="space-y-2">
      <Label htmlFor="reason-dialog-text">{label}</Label>
      <Textarea id="reason-dialog-text" rows={3} maxLength={2000} value={reason} onChange={e => setReason(e.target.value)} autoFocus />
      {minLength > 1 && <p className="text-xs text-muted-foreground">Tối thiểu {minLength} ký tự.</p>}
      <DialogFooter className="gap-2 pt-2">
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Huỷ</Button>
        <Button type="submit" variant={destructive ? 'destructive' : 'default'} disabled={tooShort || pending}>
          {pending ? 'Đang xử lý…' : confirmLabel}
        </Button>
      </DialogFooter>
    </form>
  )
}
