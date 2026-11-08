import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Eye, EyeOff, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import { ImagePicker } from '@/components/ImagePicker'
import { MAX_ADMIN_IMAGES, imageErrorMessage, isStorageFull } from '@/lib/images'
import type { LightboxImage } from '@/lib/imageTypes'
import { useDeleteImage, useSetImageVisibility, useTaskImages, useUploadTaskImages } from '@/features/admin/images/useImages'

const overlayButton = 'rounded bg-black/50 p-1 text-white hover:bg-black/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'

/** Spec §7: task images in the drawer — upload (drag & drop, several at once), grid, lightbox, share/hide, delete. */
export function TaskImages({ taskId }: { taskId: string }) {
  const navigate = useNavigate()
  const { data: images = [], isLoading, isError } = useTaskImages(taskId)
  const upload = useUploadTaskImages(taskId)
  const visibility = useSetImageVisibility()
  const remove = useDeleteImage()
  const [shareNew, setShareNew] = useState(true)
  const [open, setOpen] = useState<number | null>(null)
  const [confirmId, setConfirmId] = useState<string | null>(null)

  const items: LightboxImage[] = images.map(i => ({ id: i.id, src: i.previewUrl, thumbSrc: i.thumbUrl, alt: i.originalName }))

  function onFiles(files: File[]) {
    upload.mutate({ files, visibleToClient: shareNew }, {
      onSuccess: list => toast.success(`Đã tải ${list.length} ảnh`),
      onError: err => toast.error(imageErrorMessage(err), isStorageFull(err)
        ? { action: { label: 'Mở Dung lượng', onClick: () => navigate('/admin/settings/storage') } }
        : undefined),
    })
  }

  return (
    <section aria-labelledby="task-images-heading" className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 id="task-images-heading" className="text-sm font-semibold text-foreground">
          Hình ảnh{images.length > 0 && <span className="font-normal text-muted-foreground"> ({images.length})</span>}
        </h3>
        <label className="flex items-center gap-2 text-xs text-muted-foreground">
          <input type="checkbox" checked={shareNew} onChange={e => setShareNew(e.target.checked)} className="h-4 w-4 accent-primary" />
          Khách xem được ảnh mới tải
        </label>
      </div>
      <ImagePicker
        id="task-images-input"
        max={MAX_ADMIN_IMAGES}
        disabled={upload.isPending}
        onFiles={onFiles}
        label={upload.isPending ? 'Đang tải ảnh…' : 'Thêm ảnh'}
      />
      {isLoading ? (
        <Skeleton className="h-24 w-full" />
      ) : isError ? (
        <p className="text-sm text-muted-foreground">Không tải được ảnh.</p>
      ) : (
        <ImageGrid
          images={items}
          onOpen={setOpen}
          emptyText="Chưa có ảnh nào."
          renderOverlay={(img, i) => {
            const image = images[i]
            return (
              <div className="absolute inset-x-0 bottom-0 flex items-center justify-between gap-1 bg-gradient-to-t from-black/60 to-transparent p-1.5">
                <button
                  type="button"
                  className={overlayButton}
                  aria-label={image.visibleToClient ? `Ẩn ${img.alt} khỏi khách` : `Cho khách xem ${img.alt}`}
                  title={image.visibleToClient ? 'Khách đang xem được' : 'Đang ẩn với khách'}
                  onClick={() => visibility.mutate(
                    { id: image.id, visibleToClient: !image.visibleToClient },
                    { onError: err => toast.error(imageErrorMessage(err)) },
                  )}
                >
                  {image.visibleToClient ? <Eye className="h-3.5 w-3.5" aria-hidden="true" /> : <EyeOff className="h-3.5 w-3.5" aria-hidden="true" />}
                </button>
                <button type="button" className={overlayButton} aria-label={`Xoá ${img.alt}`} onClick={() => setConfirmId(image.id)}>
                  <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                </button>
              </div>
            )
          }}
        />
      )}
      <ImageLightbox images={items} index={open} onIndexChange={setOpen} />
      <ConfirmDialog
        open={confirmId !== null}
        onOpenChange={o => { if (!o) setConfirmId(null) }}
        title="Xoá ảnh?"
        description="Ảnh và các bản xem trước bị xoá khỏi kho lưu trữ, không khôi phục được."
        confirmLabel="Xoá ảnh"
        destructive
        pending={remove.isPending}
        onConfirm={() => {
          if (!confirmId) return
          remove.mutate(confirmId, {
            onSuccess: () => { setConfirmId(null); toast.success('Đã xoá ảnh') },
            onError: err => toast.error(imageErrorMessage(err)),
          })
        }}
      />
    </section>
  )
}
