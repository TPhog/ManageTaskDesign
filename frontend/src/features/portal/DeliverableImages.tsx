import { useState } from 'react'
import { ImageGrid } from '@/components/ImageGrid'
import { Button } from '@/components/ui/button'
import { imagesOfDeliverable } from './images'
import { Figure } from './kit/editorial'
import { PortalLightbox } from './PortalLightbox'
import { usePortalImages } from './usePortal'

interface Props { projectId: string; deliverableId: string; deliverableName: string }

/** CF-07: the images of the product waiting for review, right above "Duyệt". */
export function DeliverableImages({ projectId, deliverableId, deliverableName }: Props) {
  const { data } = usePortalImages(projectId)
  const [open, setOpen] = useState<number | null>(null)
  if (!data) return null
  const images = imagesOfDeliverable(data, deliverableId)
  if (images.length === 0) {
    return (
      <p className="font-serif text-sm italic text-muted-foreground">
        Chưa có ảnh riêng cho {deliverableName}. Xem các ảnh mới nhất ở mục{' '}
        <a href="#portal-images" className="link-inline not-italic">Hình ảnh</a>.
      </p>
    )
  }
  return (
    <div className="space-y-2">
      <Figure caption={`Bản cần duyệt · ${images.length} ảnh — chạm để xem lớn`}>
        <ImageGrid images={images.slice(0, 4)} onOpen={setOpen} className="grid-cols-2 gap-1.5 sm:grid-cols-2" />
      </Figure>
      {images.length > 4 && (
        <Button type="button" variant="outline" className="h-11 w-full" onClick={() => setOpen(0)}>Xem cả {images.length} ảnh</Button>
      )}
      <PortalLightbox images={images} index={open} onIndexChange={setOpen} />
    </div>
  )
}
