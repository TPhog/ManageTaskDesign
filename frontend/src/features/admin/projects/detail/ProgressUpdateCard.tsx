import { useState, type FormEvent } from 'react'
import { Eye } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { ApiError } from '@/lib/api'
import { formatDatetime } from '@/lib/format'
import { errorText } from '@/features/admin/revisions/revisionUtils'
import { useClientTimeline, usePostProgressUpdate } from '@/features/admin/revisions/useRevisions'

const MAX = 1000

/** "Cập nhật tiến độ": a note the client sees on the portal timeline (activity visible_to_client, BR-95). */
export function ProgressUpdateCard({ projectId }: { projectId: string }) {
  const [message, setMessage] = useState('')
  const post = usePostProgressUpdate(projectId)
  const { data: entries = [], isLoading, error } = useClientTimeline(projectId)

  function submit(e: FormEvent) {
    e.preventDefault()
    const text = message.trim()
    if (!text) return
    post.mutate(text, {
      onSuccess: () => { setMessage(''); toast.success('Đã gửi cập nhật cho khách') },
      onError: err => toast.error(errorText(err as ApiError, 'Không thể gửi cập nhật')),
    })
  }

  return (
    <div className="space-y-4 rounded-xl border bg-card p-5 shadow-card">
      <form onSubmit={submit} className="space-y-2">
        <Label htmlFor="progress-update">Cập nhật tiến độ cho khách</Label>
        <Textarea
          id="progress-update"
          rows={3}
          maxLength={MAX}
          value={message}
          onChange={e => setMessage(e.target.value)}
          placeholder="VD: Đã xong bản phác thảo logo, mai em gửi anh/chị duyệt."
        />
        <div className="flex items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">Khách thấy nội dung này trong Portal · {message.length}/{MAX}</p>
          <Button type="submit" size="sm" disabled={!message.trim() || post.isPending}>
            {post.isPending ? 'Đang gửi…' : 'Gửi cập nhật'}
          </Button>
        </div>
      </form>
      <div>
        <p className="mb-2 flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
          <Eye className="h-3.5 w-3.5" aria-hidden="true" />
          Khách đang thấy
        </p>
        {isLoading ? (
          <p className="text-sm text-muted-foreground">Đang tải…</p>
        ) : error ? (
          <p className="text-sm text-muted-foreground">Không tải được tiến trình.</p>
        ) : entries.length === 0 ? (
          <p className="text-sm text-muted-foreground">Chưa có mục nào hiển thị cho khách.</p>
        ) : (
          <ul className="space-y-2">
            {entries.slice(0, 10).map(a => (
              <li key={a.id} className="text-sm text-foreground">
                <span className="mr-2 text-xs tabular-nums text-muted-foreground">{formatDatetime(a.occurredAt)}</span>
                {a.clientMessage}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
