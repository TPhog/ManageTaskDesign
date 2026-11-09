import { useState } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { ImageGrid } from '@/components/ImageGrid'
import type { LightboxImage, PortalImage } from '@/lib/imageTypes'
import { groupPortalImages, toLightbox } from './images'
import { EditorialSection, Figure } from './kit/editorial'
import { PortalLightbox } from './PortalLightbox'
import { usePortalImages } from './usePortal'

/** Spec §7: "Hình ảnh" — watermarked previews of the project, grouped by product (CF-06/07: larger tiles, full-screen viewer). */
export function PortalImagesSection({ projectId, number }: { projectId: string; number?: string }) {
  const { data, isLoading, isError } = usePortalImages(projectId)
  const [viewer, setViewer] = useState<{ images: LightboxImage[]; index: number } | null>(null)
  const groups = groupPortalImages(data ?? [])

  return (
    <EditorialSection id="portal-images" number={number} title="Hình ảnh">
      {isLoading ? (
        <Skeleton className="mt-3 h-24 w-full" />
      ) : isError ? (
        <p className="mt-2 text-sm text-muted-foreground">Không tải được hình ảnh.</p>
      ) : groups.length === 0 ? (
        <p className="mt-2 font-serif text-sm italic text-muted-foreground">Nhà thiết kế chưa chia sẻ hình ảnh nào.</p>
      ) : (
        <div className="mt-3 space-y-5">
          {groups.map(g => (
            <Figure key={g.key} caption={`${g.title} · ${g.images.length} ảnh`}>
              <ImageGrid
                images={g.images}
                onOpen={index => setViewer({ images: g.images, index })}
                className="grid-cols-2 gap-1.5 sm:grid-cols-3"
              />
            </Figure>
          ))}
          <p className="text-xs text-muted-foreground">Đây là ảnh xem trước. Bản gốc được gửi khi bàn giao.</p>
        </div>
      )}
      <PortalLightbox
        images={viewer?.images ?? []}
        index={viewer?.index ?? null}
        onIndexChange={index => setViewer(index === null || !viewer ? null : { ...viewer, index })}
      />
    </EditorialSection>
  )
}

/** Photos of one progress update on the timeline. */
export function PortalImageStrip({ images }: { images: PortalImage[] }) {
  const [open, setOpen] = useState<number | null>(null)
  if (images.length === 0) return null
  const items = toLightbox(images)
  return (
    <div className="mt-2">
      <ImageGrid images={items} onOpen={setOpen} className="grid-cols-3 gap-1.5 sm:grid-cols-4" />
      <PortalLightbox images={items} index={open} onIndexChange={setOpen} />
    </div>
  )
}
