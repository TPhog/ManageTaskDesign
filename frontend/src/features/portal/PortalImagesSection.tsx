import { useState } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import type { LightboxImage, PortalImage } from '@/lib/imageTypes'
import { groupPortalImages, toLightbox } from './images'
import { usePortalImages } from './usePortal'

const panelClass = 'rounded-xl border bg-card p-4 shadow-card sm:p-5'

/** Spec §7: "Hình ảnh" — watermarked previews of the project, grouped by deliverable. */
export function PortalImagesSection({ projectId }: { projectId: string }) {
  const { data, isLoading, isError } = usePortalImages(projectId)
  const [viewer, setViewer] = useState<{ images: LightboxImage[]; index: number } | null>(null)
  const groups = groupPortalImages(data ?? [])

  return (
    <section aria-labelledby="portal-images" className={panelClass}>
      <h2 id="portal-images" className="text-sm font-semibold text-foreground">Hình ảnh</h2>
      {isLoading ? (
        <Skeleton className="mt-3 h-24 w-full" />
      ) : isError ? (
        <p className="mt-2 text-sm text-muted-foreground">Không tải được hình ảnh.</p>
      ) : groups.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">Designer chưa chia sẻ hình ảnh nào.</p>
      ) : (
        <div className="mt-3 space-y-4">
          {groups.map(g => (
            <div key={g.key}>
              <h3 className="mb-2 text-xs font-medium text-muted-foreground">{g.title}</h3>
              <ImageGrid images={g.images} onOpen={index => setViewer({ images: g.images, index })} />
            </div>
          ))}
          <p className="text-xs text-muted-foreground">Ảnh xem trước có watermark. Bản gốc được gửi khi bàn giao.</p>
        </div>
      )}
      <ImageLightbox
        images={viewer?.images ?? []}
        index={viewer?.index ?? null}
        onIndexChange={index => setViewer(index === null || !viewer ? null : { ...viewer, index })}
      />
    </section>
  )
}

/** Photos of one progress update on the timeline. */
export function PortalImageStrip({ images }: { images: PortalImage[] }) {
  const [open, setOpen] = useState<number | null>(null)
  if (images.length === 0) return null
  const items = toLightbox(images)
  return (
    <div className="mt-2">
      <ImageGrid images={items} onOpen={setOpen} className="grid-cols-4 sm:grid-cols-5" />
      <ImageLightbox images={items} index={open} onIndexChange={setOpen} />
    </div>
  )
}
