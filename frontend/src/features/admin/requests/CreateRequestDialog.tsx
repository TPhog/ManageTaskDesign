import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import { designerSources, requestSourceLabel } from '@/lib/revisionLabels'
import type { DesignerRequestSource } from '@/lib/revisionTypes'
import type { PagedResult, ProjectDetail, ProjectListItem } from '@/lib/types'
import { errorText } from '@/features/admin/revisions/revisionUtils'
import { useCreateRequest } from '@/features/admin/revisions/useRevisions'

// Radix Select reserves '' for "no value"
const NO_DELIVERABLE = '_none'

interface Props {
  open: boolean
  onClose: () => void
  onCreated: (requestId: string) => void
}

/** REQ-02: the designer records a request received over Zalo / email / phone. */
export function CreateRequestDialog({ open, onClose, onCreated }: Props) {
  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Ghi nhận yêu cầu</DialogTitle>
          <DialogDescription>Yêu cầu khách gửi qua Zalo, email hay điện thoại — ghi lại để tiếp nhận thành vòng sửa.</DialogDescription>
        </DialogHeader>
        <CreateRequestForm onClose={onClose} onCreated={onCreated} />
      </DialogContent>
    </Dialog>
  )
}

function CreateRequestForm({ onClose, onCreated }: Omit<Props, 'open'>) {
  const [projectId, setProjectId] = useState('')
  const [deliverableId, setDeliverableId] = useState(NO_DELIVERABLE)
  const [source, setSource] = useState<DesignerRequestSource>('ZALO')
  const [content, setContent] = useState('')
  const create = useCreateRequest()

  const { data: projects } = useQuery({
    queryKey: [...qk.projects.all, 'request-picker'],
    queryFn: () => api.get<PagedResult<ProjectListItem>>('/admin/projects?pageSize=100'),
  })
  const { data: project } = useQuery({
    queryKey: qk.projects.detail(projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${projectId}`),
    enabled: !!projectId,
  })
  const choices = (projects?.items ?? []).filter(p => p.status !== 'CANCELLED')
  const deliverables = (project?.deliverables ?? []).filter(d => d.status !== 'CANCELLED')

  function submit(e: FormEvent) {
    e.preventDefault()
    if (!projectId) { toast.error('Chọn dự án'); return }
    if (!content.trim()) { toast.error('Nhập nội dung yêu cầu'); return }
    create.mutate(
      { projectId, body: { content: content.trim(), source, deliverableId: deliverableId === NO_DELIVERABLE ? null : deliverableId } },
      {
        onSuccess: detail => {
          toast.success('Đã ghi nhận yêu cầu')
          onClose()
          onCreated(detail.request.id)
        },
        onError: err => toast.error(errorText(err as ApiError, 'Không thể ghi nhận yêu cầu')),
      },
    )
  }

  return (
    <form onSubmit={submit} className="space-y-4 py-2" noValidate>
      <div className="space-y-2">
        <Label htmlFor="request-project">Dự án</Label>
        <Select value={projectId} onValueChange={v => { setProjectId(v); setDeliverableId(NO_DELIVERABLE) }}>
          <SelectTrigger id="request-project">
            <SelectValue placeholder="Chọn dự án" />
          </SelectTrigger>
          <SelectContent>
            {choices.map(p => <SelectItem key={p.id} value={p.id}>{p.name} — {p.client.name}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="request-deliverable">Hạng mục (tuỳ chọn)</Label>
          <Select value={deliverableId} onValueChange={setDeliverableId} disabled={!projectId}>
            <SelectTrigger id="request-deliverable">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NO_DELIVERABLE}>Không chọn</SelectItem>
              {deliverables.map(d => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="request-source">Nguồn</Label>
          <Select value={source} onValueChange={v => setSource(v as DesignerRequestSource)}>
            <SelectTrigger id="request-source">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {designerSources.map(s => <SelectItem key={s} value={s}>{requestSourceLabel[s]}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="space-y-2">
        <Label htmlFor="request-content">Nội dung yêu cầu</Label>
        <Textarea
          id="request-content"
          rows={5}
          maxLength={5000}
          placeholder="Mỗi ý một dòng"
          value={content}
          onChange={e => setContent(e.target.value)}
        />
      </div>
      <DialogFooter className="gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="submit" disabled={create.isPending}>{create.isPending ? 'Đang lưu…' : 'Ghi nhận'}</Button>
      </DialogFooter>
    </form>
  )
}
