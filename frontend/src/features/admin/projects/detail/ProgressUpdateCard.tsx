import { useMemo, useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Copy, Eye, X } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import { ImagePicker } from '@/components/ImagePicker'
import { api, type ApiError } from '@/lib/api'
import { formatDatetime } from '@/lib/format'
import { MAX_ADMIN_IMAGES, duplicateNotice, imageErrorMessage, isStorageFull } from '@/lib/images'
import type { LightboxImage } from '@/lib/imageTypes'
import { portalInviteMessage, usePortalLinkForClient } from '@/lib/portalLink'
import { qk } from '@/lib/queryKeys'
import type { ClientDetail, ProjectDetail } from '@/lib/types'
import { errorText } from '@/features/admin/revisions/revisionUtils'
import { useClientTimeline, usePostProgressUpdate } from '@/features/admin/revisions/useRevisions'
import { useProjectImages } from '@/features/admin/images/useImages'
import { copyWhenReady } from '@/lib/clipboard'

const MAX = 1000

/** "Cập nhật tiến độ": a note (+ photos, plan 08) the client sees on the portal timeline (BR-95). */
export function ProgressUpdateCard({ projectId }: { projectId: string }) {
  const navigate = useNavigate()
  const [message, setMessage] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [viewer, setViewer] = useState<{ images: LightboxImage[]; index: number } | null>(null)
  const [justSent, setJustSent] = useState(false)
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
      onSuccess: result => {
        setMessage('')
        setFiles([])
        setJustSent(true)
        toast.success('Đã gửi cập nhật cho khách')
        const duplicate = duplicateNotice(result.images ?? [])
        if (duplicate) toast.warning(duplicate)
      },
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
          hint={`Ảnh hiển thị dạng thu nhỏ; khách bấm để xem rõ hơn · tối đa ${MAX_ADMIN_IMAGES} ảnh`}
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
      {justSent && <ShareProgressLink projectId={projectId} />}
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

/**
 * After a progress update: copy "link theo dõi tiến độ" for Zalo. The server keeps only a hash of the portal link,
 * so copying means creating a new link; a client who already holds one is warned that the old link stops working.
 */
function ShareProgressLink({ projectId }: { projectId: string }) {
  const [asking, setAsking] = useState(false)
  const { data: project } = useQuery({
    queryKey: qk.projects.detail(projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${projectId}`),
  })
  const clientId = project?.client.id
  const { data: client } = useQuery({
    queryKey: qk.clients.detail(clientId ?? ''),
    queryFn: () => api.get<ClientDetail>(`/admin/clients/${clientId}`),
    enabled: !!clientId,
  })
  const portal = usePortalLinkForClient(clientId)
  if (!client) return null

  async function copyLink() {
    setAsking(false)
    const { text, copied } = await copyWhenReady(portal.create().then(url => (url ? portalInviteMessage(client!.name, url) : null)))
    if (text === null) return
    if (copied) toast.success('Đã sao chép tin nhắn kèm link portal — dán vào Zalo cho khách')
    else toast.error('Không copy được, hãy tạo link ở trang Khách hàng')
  }

  if (client.hasPortalLink && !client.portalEnabled) {
    return <p className="text-xs text-muted-foreground">Portal của khách đang tắt — bật lại ở Khách hàng để khách xem được cập nhật.</p>
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button type="button" size="sm" variant="outline" disabled={portal.isPending} onClick={() => (client.hasPortalLink ? setAsking(true) : void copyLink())}>
        <Copy aria-hidden="true" />
        Sao chép tin nhắn + link portal
      </Button>
      <ConfirmDialog
        open={asking}
        onOpenChange={setAsking}
        title="Tạo link mới để gửi khách?"
        description="Link cũ khách đang giữ sẽ ngừng dùng được. Tiếp tục?"
        confirmLabel="Tạo link mới và sao chép"
        pending={portal.isPending}
        onConfirm={() => void copyLink()}
      />
    </div>
  )
}
