import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, FileText, Lock, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { EmptyState } from '@/components/EmptyState'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { StatusBadge } from '@/components/StatusBadge'
import { api, type ApiError } from '@/lib/api'
import { formatDate } from '@/lib/format'
import type { Brief } from '@/lib/types'

interface Props {
  projectId: string
  brief: Brief | null
}

/** Parent renders this with key={brief.id + brief.updatedAt} so local draft state resets after each save. */
export function BriefTab({ projectId, brief }: Props) {
  const qc = useQueryClient()
  const [content, setContent] = useState(brief?.content ?? '')
  const refresh = () => qc.invalidateQueries({ queryKey: ['projects', projectId] })
  const dirty = !!brief && content !== brief.content
  const empty = content.trim() === ''
  const [asking, setAsking] = useState(false)

  const saveMutation = useMutation({
    mutationFn: () => api.put(`/admin/briefs/${brief!.id}`, { content }),
    onSuccess: () => {
      refresh()
      toast.success('Đã lưu brief')
    },
    onError: (err: ApiError) =>
      toast.error(err.code === 'BRIEF_LOCKED' ? 'Brief đã xác nhận, không thể sửa' : 'Không thể lưu brief'),
  })

  const confirmMutation = useMutation({
    // save pending edits first so the confirmed version is what the designer sees
    mutationFn: async () => {
      if (dirty) await api.put(`/admin/briefs/${brief!.id}`, { content })
      return api.post(`/admin/briefs/${brief!.id}/confirm`, {})
    },
    onSuccess: () => {
      setAsking(false)
      refresh()
      toast.success('Đã xác nhận brief')
    },
    // the save step may have succeeded before confirm failed, so refresh the cache either way
    onError: (err: ApiError) => {
      setAsking(false)
      refresh()
      toast.error(err.detail ?? 'Không thể xác nhận brief')
    },
  })

  const newVersionMutation = useMutation({
    mutationFn: () => api.post(`/admin/projects/${projectId}/briefs`, {}),
    onSuccess: () => {
      refresh()
      toast.success('Đã tạo phiên bản mới')
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể tạo phiên bản mới'),
  })

  if (!brief) {
    return (
      <div className="rounded-xl border bg-card shadow-card">
        <EmptyState icon={FileText} title="Chưa có brief" description="Brief được tạo tự động khi tạo dự án." />
      </div>
    )
  }

  const confirmed = brief.status === 'CONFIRMED'
  const busy = saveMutation.isPending || confirmMutation.isPending

  return (
    <div className="space-y-4 rounded-xl border bg-card p-5 shadow-card">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span className="font-medium text-foreground">Phiên bản {brief.version}</span>
          <StatusBadge status={brief.status} />
          {brief.confirmedAt && <span>· Xác nhận {formatDate(brief.confirmedAt)}</span>}
        </div>
        {confirmed && (
          <Button variant="outline" size="sm" onClick={() => newVersionMutation.mutate()} disabled={newVersionMutation.isPending}>
            <Plus aria-hidden="true" />
            Tạo phiên bản mới
          </Button>
        )}
      </div>

      {confirmed ? (
        <>
          <div className="flex items-center gap-2 rounded-lg bg-muted px-3 py-2 text-xs text-muted-foreground">
            <Lock className="h-3.5 w-3.5" aria-hidden="true" />
            Brief đã xác nhận nên không sửa được. Tạo phiên bản mới để thay đổi.
          </div>
          {brief.content ? (
            <div className="whitespace-pre-wrap text-sm leading-relaxed text-foreground">{brief.content}</div>
          ) : (
            <p className="text-sm italic text-muted-foreground">Brief chưa có nội dung</p>
          )}
        </>
      ) : (
        <>
          <div className="space-y-2">
            <Label htmlFor="brief-content">Nội dung brief</Label>
            <Textarea
              id="brief-content"
              rows={12}
              value={content}
              onChange={e => setContent(e.target.value)}
              placeholder="Mục tiêu, đối tượng, phong cách, màu sắc, định dạng bàn giao…"
              className="font-[inherit] leading-relaxed"
            />
            {dirty && <p className="text-xs text-muted-foreground">Có thay đổi chưa lưu</p>}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="outline" onClick={() => saveMutation.mutate()} disabled={!dirty || busy}>
              {saveMutation.isPending ? 'Đang lưu…' : 'Lưu brief'}
            </Button>
            <Button onClick={() => setAsking(true)} disabled={busy || empty} aria-describedby={empty ? 'brief-empty-hint' : undefined}>
              <CheckCircle2 aria-hidden="true" />
              Xác nhận brief
            </Button>
          </div>
          {empty && (
            <p id="brief-empty-hint" className="text-right text-xs text-muted-foreground">Nhập nội dung trước khi xác nhận.</p>
          )}
          <ConfirmDialog
            open={asking}
            onOpenChange={setAsking}
            title={`Xác nhận brief phiên bản ${brief.version}?`}
            description="Brief đã xác nhận không sửa được (muốn đổi phải tạo phiên bản mới) và khách sẽ thấy nội dung này trên portal."
            confirmLabel="Xác nhận"
            pending={confirmMutation.isPending}
            onConfirm={() => confirmMutation.mutate()}
          />
        </>
      )}
    </div>
  )
}
