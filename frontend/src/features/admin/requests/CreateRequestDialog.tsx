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
import { SearchCombobox } from '@/components/SearchCombobox'
import { api, type ApiError } from '@/lib/api'
import { useProjectOptions } from '@/lib/pickers'
import { qk } from '@/lib/queryKeys'
import { designerSources, requestSourceLabel } from '@/lib/revisionLabels'
import type { DesignerRequestSource } from '@/lib/revisionTypes'
import type { ProjectDetail } from '@/lib/types'
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
  const [projectLabel, setProjectLabel] = useState('')
  const [projectSearch, setProjectSearch] = useState('')
  const [deliverableId, setDeliverableId] = useState(NO_DELIVERABLE)
  const [source, setSource] = useState<DesignerRequestSource>('ZALO')
  const [content, setContent] = useState('')
  const [errors, setErrors] = useState<{ project?: string; content?: string }>({})
  const create = useCreateRequest()
  // QA D-07/D-28: searchable by project or client name, newest first, not capped at 100
  const projects = useProjectOptions(projectSearch)

  const { data: project } = useQuery({
    queryKey: qk.projects.detail(projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${projectId}`),
    enabled: !!projectId,
  })
  const deliverables = (project?.deliverables ?? []).filter(d => d.status !== 'CANCELLED')

  function submit(e: FormEvent) {
    e.preventDefault()
    const next = { project: projectId ? undefined : 'Chọn dự án', content: content.trim() ? undefined : 'Nhập nội dung yêu cầu' }
    setErrors(next)
    if (next.project || next.content) return
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
        <SearchCombobox
          id="request-project"
          value={projectId}
          selectedLabel={projectLabel}
          options={projects.options}
          loading={projects.loading}
          search={projectSearch}
          onSearchChange={setProjectSearch}
          onSelect={o => {
            setProjectId(o?.value ?? '')
            setErrors(prev => ({ ...prev, project: undefined }))
            setProjectLabel(o ? `${o.label}${o.hint ? ` — ${o.hint}` : ''}` : '')
            setDeliverableId(NO_DELIVERABLE)
          }}
          placeholder="Chọn dự án"
          searchPlaceholder="Tìm dự án hoặc khách…"
          emptyText="Không có dự án khớp"
          invalid={!!errors.project}
          describedBy={errors.project ? 'request-project-error' : undefined}
        />
        {errors.project && <p id="request-project-error" role="alert" className="text-sm text-destructive">{errors.project}</p>}
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
          onChange={e => { setContent(e.target.value); setErrors(prev => ({ ...prev, content: undefined })) }}
          aria-invalid={!!errors.content || undefined}
          aria-describedby={errors.content ? 'request-content-error' : undefined}
        />
        {errors.content && <p id="request-content-error" role="alert" className="text-sm text-destructive">{errors.content}</p>}
      </div>
      <DialogFooter className="gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="submit" disabled={create.isPending}>{create.isPending ? 'Đang lưu…' : 'Ghi nhận'}</Button>
      </DialogFooter>
    </form>
  )
}
