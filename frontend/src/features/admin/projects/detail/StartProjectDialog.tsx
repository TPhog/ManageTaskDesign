import { useState, type FormEvent } from 'react'
import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateInput } from '@/components/DateInput'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import type { ProjectDetail } from '@/lib/types'
import type { FlowWarning, StartPreviewItem } from '@/lib/flowTypes'
import { useStartPreview, useStartProject } from '@/features/admin/flow/useFlow'
import { draftsFromPreview, toStartTasks, type StartDraft } from '@/features/admin/flow/startProject'

interface Props {
  project: ProjectDetail
  open: boolean
  onOpenChange: (open: boolean) => void
  /** current flow warnings (e.g. deposit not received) — shown, never blocking (F4) */
  warnings: FlowWarning[]
}

/** Plan 06b F3: preview the template tasks per NOT_STARTED deliverable, untick or rename, then start. */
export function StartProjectDialog({ project, open, onOpenChange, warnings }: Props) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Bắt đầu dự án</DialogTitle>
          <DialogDescription>
            Các hạng mục chưa bắt đầu sẽ chuyển sang Đang làm. Công việc được đề xuất theo mẫu của dịch vụ — bỏ chọn hoặc sửa tên trước khi tạo.
          </DialogDescription>
        </DialogHeader>
        {/* mounted per open: each open fetches a fresh preview once and seeds the form from it; a later refetch can't reset edits */}
        {open && <StartPreviewBody project={project} warnings={warnings} onClose={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  )
}

function StartPreviewBody({ project, warnings, onClose }: { project: ProjectDetail; warnings: FlowWarning[]; onClose: () => void }) {
  const preview = useStartPreview(project.id, true)
  // StartForm seeds its drafts once on mount (no key): later refetches/invalidations can't reset edits.
  if (preview.isFetchedAfterMount && preview.data) {
    return <StartForm project={project} preview={preview.data} warnings={warnings} onClose={onClose} />
  }
  if (preview.isError) return <p className="text-sm text-muted-foreground">Không tải được danh sách công việc mẫu.</p>
  return <Skeleton className="h-32 w-full" />
}

interface FormProps {
  project: ProjectDetail
  preview: StartPreviewItem[]
  warnings: FlowWarning[]
  onClose: () => void
}

function StartForm({ project, preview, warnings, onClose }: FormProps) {
  const [drafts, setDrafts] = useState<StartDraft[]>(() => draftsFromPreview(preview))
  const start = useStartProject(project.id)
  const tasks = toStartTasks(drafts)
  const count = tasks.reduce((n, t) => n + t.titles.length, 0)

  function patch(deliverableIndex: number, titleIndex: number, change: Partial<StartDraft['titles'][number]>) {
    setDrafts(prev =>
      prev.map((d, i) =>
        i !== deliverableIndex
          ? d
          : { ...d, titles: d.titles.map((t, j) => (j === titleIndex ? { ...t, ...change } : t)) },
      ),
    )
  }

  function patchDue(deliverableIndex: number, dueDate: string) {
    setDrafts(prev => prev.map((d, i) => (i === deliverableIndex ? { ...d, dueDate } : d)))
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    start.mutate({ rowVersion: project.rowVersion, tasks }, { onSuccess: onClose })
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      {warnings.length > 0 && (
        <ul aria-label="Cảnh báo khi bắt đầu" className="space-y-1.5">
          {warnings.map((w, i) => (
            <li key={`${w.kind}-${i}`} className="flex items-start gap-2 rounded-lg border border-warning/30 bg-warning-soft px-3 py-2 text-sm text-warning">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              {w.message}
            </li>
          ))}
        </ul>
      )}
      {drafts.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không có hạng mục nào ở trạng thái Chưa bắt đầu.</p>
      ) : (
        drafts.map((d, di) => (
          <fieldset key={d.deliverableId} className="space-y-2 rounded-lg border p-3">
            <legend className="px-1 text-sm font-semibold text-foreground">
              {d.name}
              {d.serviceName && <span className="font-normal text-muted-foreground"> · {d.serviceName}</span>}
            </legend>
            {d.titles.length === 0 ? (
              <p className="text-xs text-muted-foreground">Dịch vụ chưa có bước mẫu — thêm công việc sau trong tab Công việc.</p>
            ) : (
              <>
              <div className="space-y-1">
                <Label htmlFor={`start-due-${d.deliverableId}`}>Hạn chung · {d.name}</Label>
                <DateInput id={`start-due-${d.deliverableId}`} value={d.dueDate} onChange={iso => patchDue(di, iso)} />
                <p className="text-xs text-muted-foreground">Áp cho mọi công việc bên dưới; sửa riêng từng việc sau trong tab Công việc.</p>
              </div>
              {d.titles.map((t, ti) => (
                <div key={ti} className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={t.checked}
                    onChange={e => patch(di, ti, { checked: e.target.checked })}
                    aria-label={`Tạo công việc ${ti + 1} của ${d.name}`}
                    className="h-4 w-4 shrink-0 accent-primary"
                  />
                  <Input
                    value={t.title}
                    maxLength={200}
                    disabled={!t.checked}
                    onChange={e => patch(di, ti, { title: e.target.value })}
                    aria-label={`Tên công việc ${ti + 1} của ${d.name}`}
                  />
                </div>
              ))}
              </>
            )}
          </fieldset>
        ))
      )}
      <DialogFooter className="gap-2">
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="submit" disabled={start.isPending}>
          {start.isPending ? 'Đang bắt đầu…' : count > 0 ? `Bắt đầu dự án · tạo ${count} việc` : 'Bắt đầu dự án'}
        </Button>
      </DialogFooter>
    </form>
  )
}
