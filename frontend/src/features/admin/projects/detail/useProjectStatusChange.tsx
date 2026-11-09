import { useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { TriangleAlert } from 'lucide-react'
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
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { ProjectDetail, ProjectStatus } from '@/lib/types'
import { confirmableWarnings, reasonCopy, statusErrorMessage, type ProjectTransition } from './projectStatus'

interface StatusBody {
  to: ProjectStatus
  reason?: string
  reasonPublic?: boolean
  confirmWarnings?: boolean
  rowVersion: number
}

/**
 * QA D-01: the one status-change flow, used by the status menu and by the COMPLETE_PROJECT flow action.
 * Asks the required reason, then shows the server's warnings (422 BUSINESS_RULE) for confirmation (confirmWarnings).
 */
export function useProjectStatusChange(project: ProjectDetail): { request: (t: ProjectTransition) => void; pending: boolean; dialogs: ReactNode } {
  const qc = useQueryClient()
  const [asking, setAsking] = useState<ProjectTransition | null>(null)
  const [reason, setReason] = useState('')
  const [reasonPublic, setReasonPublic] = useState(false)
  const [pending, setPending] = useState<{ transition: ProjectTransition; body: StatusBody; warnings: string[] } | null>(null)

  const mutation = useMutation({
    mutationFn: (body: StatusBody) => api.post(`/admin/projects/${project.id}/status`, body),
  })

  function send(transition: ProjectTransition, body: StatusBody) {
    mutation.mutate(body, {
      onSuccess: () => {
        void qc.invalidateQueries({ queryKey: qk.projects.all })
        void qc.invalidateQueries({ queryKey: qk.tasks.lists })
        void qc.invalidateQueries({ queryKey: qk.tasks.calendarAll })
        void qc.invalidateQueries({ queryKey: qk.revisions.all })
        toast.success(`Đã chuyển dự án: ${transition.label}`)
        setAsking(null)
        setPending(null)
      },
      onError: err => {
        const warnings = confirmableWarnings(err as ApiError)
        if (warnings) {
          setAsking(null)
          setPending({ transition, body, warnings })
          return
        }
        toast.error(statusErrorMessage(err as ApiError))
      },
    })
  }

  function request(t: ProjectTransition) {
    if (t.reason === 'none') {
      send(t, { to: t.to, rowVersion: project.rowVersion })
      return
    }
    setReason('')
    setReasonPublic(false)
    setAsking(t)
  }

  function submitReason(e: FormEvent) {
    e.preventDefault()
    if (!asking || !reason.trim()) return
    send(asking, {
      to: asking.to,
      reason: reason.trim(),
      reasonPublic: asking.reason === 'hold' ? reasonPublic : undefined,
      rowVersion: project.rowVersion,
    })
  }

  const copy = asking && asking.reason !== 'none' ? reasonCopy[asking.reason] : null

  const dialogs = (
    <>
      <Dialog open={!!asking} onOpenChange={o => { if (!o) setAsking(null) }}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{copy?.title}</DialogTitle>
            <DialogDescription>{copy?.description}</DialogDescription>
          </DialogHeader>
          <form onSubmit={submitReason} className="space-y-3">
            <Label htmlFor="project-status-reason">{copy?.label}</Label>
            <Textarea
              id="project-status-reason"
              rows={3}
              maxLength={2000}
              value={reason}
              onChange={e => setReason(e.target.value)}
              autoFocus
            />
            {asking?.reason === 'hold' && (
              <label className="flex items-center gap-2 text-sm text-foreground">
                <input type="checkbox" checked={reasonPublic} onChange={e => setReasonPublic(e.target.checked)} />
                Hiện lý do này cho khách trên portal
              </label>
            )}
            <DialogFooter className="gap-2 pt-2">
              <Button type="button" variant="outline" onClick={() => setAsking(null)}>Huỷ</Button>
              <Button type="submit" variant={asking?.destructive ? 'destructive' : 'default'} disabled={!reason.trim() || mutation.isPending}>
                {mutation.isPending ? 'Đang xử lý…' : asking?.label}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={!!pending} onOpenChange={o => { if (!o) setPending(null) }}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Cần xác nhận: {pending?.transition.label}</DialogTitle>
            <DialogDescription>Hệ thống chỉ cảnh báo, không chặn. Kiểm tra các điểm dưới đây trước khi tiếp tục.</DialogDescription>
          </DialogHeader>
          <ul className="space-y-2">
            {pending?.warnings.map(w => (
              <li key={w} className="flex items-start gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
                <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                {w}
              </li>
            ))}
          </ul>
          <DialogFooter className="gap-2">
            {pending?.warnings.some(w => w.includes('công việc chưa xong')) && (
              <Button asChild variant="outline">
                <Link to={`/admin/projects/${project.id}?tab=tasks`} onClick={() => setPending(null)}>Xem công việc</Link>
              </Button>
            )}
            <Button type="button" variant="outline" onClick={() => setPending(null)}>Để sau</Button>
            <Button
              type="button"
              disabled={mutation.isPending}
              onClick={() => pending && send(pending.transition, { ...pending.body, confirmWarnings: true })}
            >
              {mutation.isPending ? 'Đang xử lý…' : 'Vẫn tiếp tục'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )

  return { request, pending: mutation.isPending, dialogs }
}
