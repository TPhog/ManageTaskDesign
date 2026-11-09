import { useState, type FormEvent } from 'react'
import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { isHttpUrl } from './flowUtils'

interface BaseProps {
  deliverable: { id: string; name: string }
  open: boolean
  onOpenChange: (open: boolean) => void
  pending: boolean
}

const MESSAGE_MAX = 500
const URL_MAX = 500

function emptyToNull(value: string): string | null {
  const v = value.trim()
  return v === '' ? null : v
}

interface SubmitReviewProps extends BaseProps {
  openTasks: { total: number; shown: string[]; more: number }
  onConfirm: (clientMessage: string | null) => void
}

/** Sending for review never blocks on unfinished tasks (F4) — it only warns. */
export function SubmitReviewDialog({ deliverable, open, onOpenChange, openTasks, pending, onConfirm }: SubmitReviewProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Gửi hạng mục cho khách duyệt?</DialogTitle>
          <DialogDescription>
            {deliverable.name} · Khách sẽ thấy hạng mục này ở trạng thái Chờ duyệt trên portal.
          </DialogDescription>
        </DialogHeader>
        {open && <SubmitReviewBody openTasks={openTasks} pending={pending} onCancel={() => onOpenChange(false)} onConfirm={onConfirm} />}
      </DialogContent>
    </Dialog>
  )
}

function SubmitReviewBody({ openTasks, pending, onCancel, onConfirm }: Pick<SubmitReviewProps, 'openTasks' | 'pending' | 'onConfirm'> & { onCancel: () => void }) {
  const [message, setMessage] = useState('')
  const warn = openTasks.total > 0

  function submit(e: FormEvent) {
    e.preventDefault()
    onConfirm(emptyToNull(message))
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      {warn && (
        <p className="flex items-start gap-2 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span>
            Còn {openTasks.total} công việc chưa xong: {openTasks.shown.join(', ')}
            {openTasks.more > 0 ? ` … (+${openTasks.more})` : ''}. Bạn vẫn gửi được.
          </span>
        </p>
      )}
      <div className="space-y-1.5">
        <Label htmlFor="submit-review-message">Lời nhắn cho khách (tuỳ chọn)</Label>
        <Textarea id="submit-review-message" rows={3} maxLength={MESSAGE_MAX} value={message} onChange={e => setMessage(e.target.value)} />
      </div>
      <DialogFooter className="gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>Huỷ</Button>
        <Button type="submit" disabled={pending}>
          {pending ? 'Đang gửi…' : warn ? 'Vẫn gửi duyệt' : 'Gửi duyệt'}
        </Button>
      </DialogFooter>
    </form>
  )
}

interface ApproveProps extends Omit<BaseProps, 'deliverable'> {
  deliverable: { id: string; name: string; clientDecision: 'APPROVED' | null }
  onConfirm: () => void
}

export function ApproveDialog({ deliverable, open, onOpenChange, pending, onConfirm }: ApproveProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Xác nhận hạng mục đã được duyệt?</DialogTitle>
          <DialogDescription>
            {deliverable.name} ·{' '}
            {deliverable.clientDecision === 'APPROVED'
              ? 'Khách đã duyệt trên portal — xác nhận để chuyển hạng mục sang Đã duyệt.'
              : 'Bạn ghi nhận hộ khách (ví dụ khách duyệt qua Zalo). Khách sẽ thấy hạng mục là Đã duyệt.'}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Huỷ</Button>
          <Button type="button" disabled={pending} onClick={onConfirm}>
            {pending ? 'Đang xử lý…' : 'Xác nhận đã duyệt'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface DeliverProps extends BaseProps {
  serverError?: string
  onConfirm: (v: { deliveryUrl: string | null; clientMessage: string | null }) => void
}

export function DeliverDialog({ deliverable, open, onOpenChange, pending, serverError, onConfirm }: DeliverProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Bàn giao hạng mục?</DialogTitle>
          <DialogDescription>
            {deliverable.name} · Khách sẽ thấy lời nhắn và link này trên timeline portal.
          </DialogDescription>
        </DialogHeader>
        {open && <DeliverBody pending={pending} serverError={serverError} onCancel={() => onOpenChange(false)} onConfirm={onConfirm} />}
      </DialogContent>
    </Dialog>
  )
}

function DeliverBody({ pending, serverError, onCancel, onConfirm }: Pick<DeliverProps, 'pending' | 'serverError' | 'onConfirm'> & { onCancel: () => void }) {
  const [url, setUrl] = useState('')
  const [message, setMessage] = useState('')
  const [touched, setTouched] = useState(false)
  const trimmed = url.trim()
  const urlError = touched && !isHttpUrl(trimmed)
    ? trimmed.length > URL_MAX ? 'Link tối đa 500 ký tự' : 'Link phải bắt đầu bằng http:// hoặc https://'
    : undefined
  const error = urlError ?? serverError

  function submit(e: FormEvent) {
    e.preventDefault()
    setTouched(true)
    if (!isHttpUrl(trimmed)) return
    onConfirm({ deliveryUrl: emptyToNull(trimmed), clientMessage: emptyToNull(message) })
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor="deliver-url">Link file bàn giao (tuỳ chọn)</Label>
        <Input
          id="deliver-url"
          value={url}
          onChange={e => setUrl(e.target.value)}
          placeholder="https://drive.google.com/…"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'deliver-url-error' : undefined}
        />
        {error && <p id="deliver-url-error" role="alert" className="text-sm text-destructive">{error}</p>}
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="deliver-message">Lời nhắn cho khách (tuỳ chọn)</Label>
        <Textarea id="deliver-message" rows={3} maxLength={MESSAGE_MAX} value={message} onChange={e => setMessage(e.target.value)} />
      </div>
      <DialogFooter className="gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>Huỷ</Button>
        <Button type="submit" disabled={pending}>{pending ? 'Đang bàn giao…' : 'Bàn giao'}</Button>
      </DialogFooter>
    </form>
  )
}
