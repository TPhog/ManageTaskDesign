import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Eye, X } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import { ImagePicker } from '@/components/ImagePicker'
import type { ApiError } from '@/lib/api'
import { formatDatetime } from '@/lib/format'
import { MAX_ADMIN_IMAGES, imageErrorMessage, isStorageFull } from '@/lib/images'
import type { LightboxImage } from '@/lib/imageTypes'
import { errorText } from '@/features/admin/revisions/revisionUtils'
import { useClientTimeline, usePostProgressUpdate } from '@/features/admin/revisions/useRevisions'
import { useProjectImages } from '@/features/admin/images/useImages'

const MAX = 1000

/** "Cập nhật tiến độ": a note (+ photos, plan 08) the client sees on the portal timeline (BR-95). */
export function ProgressUpdateCard({ projectId }: { projectId: string }) {
  const navigate = useNavigate()
  const [message, setMessage] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [viewer, setViewer] = useState<{ images: LightboxImage[]; index: number } | null>(null)
  const post = usePostProgressUpdate(projectId)
  const { data: entries = [], isLoading, error } = useClientTimeline(projectId)
  const { data: progressImages = [] } = useProjectImages(projectId, 'PROGRESS_UPDATE')

  const imagesByUpdate = useMemo(() => {
    const map = new Map<string, LightboxImage[]>()
    for (const i of progressImages) {
      const list = map.get(i.ownerId) ?? []
      list.push({ id: i.id, src: i.previewUrl, thumbSrc: i.thumbUrl, alt: i.originalName })
      map.set(i.ownerId, list)
    }
    return map
  }, [progressImages])

  function addFiles(picked: File[]) {
    setFiles(current => [...current, ...picked].slice(0, MAX_ADMIN_IMAGES))
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    const text = message.trim()
    if (!text) return
    post.mutate({ message: text, files }, {
      onSuccess: () => { setMessage(''); setFiles([]); toast.success('Đã gửi cập nhật cho khách') },
      onError: err => files.length > 0
        ? toast.error(imageErrorMessage(err), isStorageFull(err)
          ? { action: { label: 'Mở Dung lượng', onClick: () => navigate('/admin/settings/storage') } }
          : undefined)
        : toast.error(errorText(err as ApiError, 'Không thể gửi cập nhật')),
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
        <ImagePicker
          id="progress-update-images"
          max={MAX_ADMIN_IMAGES - files.length}
          disabled={post.isPending || files.length >= MAX_ADMIN_IMAGES}
          onFiles={addFiles}
          label="Đính kèm ảnh (tuỳ chọn)"
          hint={`Khách xem bản xem trước · tối đa ${MAX_ADMIN_IMAGES} ảnh`}
        />
        {files.length > 0 && (
          <ul className="flex flex-wrap gap-1.5" aria-label="Ảnh sẽ gửi">
            {files.map((f, i) => (
              <li key={`${f.name}-${i}`} className="inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs">
                {f.name}
                <button type="button" aria-label={`Bỏ ${f.name}`} onClick={() => setFiles(files.filter((_, j) => j !== i))}>
                  <X className="h-3 w-3" aria-hidden="true" />
                </button>
              </li>
            ))}
          </ul>
        )}
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
            {entries.slice(0, 10).map(a => {
              const images = a.entityType === 'progress_update' ? imagesByUpdate.get(a.entityId) ?? [] : []
              return (
                <li key={a.id} className="space-y-1.5 text-sm text-foreground">
                  <p>
                    <span className="mr-2 text-xs tabular-nums text-muted-foreground">{formatDatetime(a.occurredAt)}</span>
                    {a.clientMessage}
                  </p>
                  {images.length > 0 && (
                    <ImageGrid images={images} onOpen={index => setViewer({ images, index })} className="grid-cols-6 sm:grid-cols-8" />
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </div>
      <ImageLightbox
        images={viewer?.images ?? []}
        index={viewer?.index ?? null}
        onIndexChange={index => setViewer(index === null || !viewer ? null : { ...viewer, index })}
      />
    </div>
  )
}
