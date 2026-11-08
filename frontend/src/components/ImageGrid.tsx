import type { ReactNode } from 'react'
import type { LightboxImage } from '@/lib/imageTypes'
import { cn } from '@/lib/utils'

interface Props {
  images: LightboxImage[]
  onOpen: (index: number) => void
  /** per-image controls drawn over the thumbnail (e.g. hide / delete) */
  renderOverlay?: (image: LightboxImage, index: number) => ReactNode
  className?: string
  emptyText?: string
}

/** Square thumbnail grid; a click opens the lightbox at that index. */
export function ImageGrid({ images, onOpen, renderOverlay, className, emptyText }: Props) {
  if (images.length === 0) return emptyText ? <p className="text-sm text-muted-foreground">{emptyText}</p> : null
  return (
    <ul className={cn('grid grid-cols-3 gap-2 sm:grid-cols-4', className)}>
      {images.map((img, i) => (
        <li key={img.id} className="relative aspect-square overflow-hidden rounded-lg border bg-muted">
          <button
            type="button"
            onClick={() => onOpen(i)}
            aria-label={`Xem ảnh ${img.alt}`}
            className="block h-full w-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <img
              src={img.thumbSrc}
              alt={img.alt}
              loading="lazy"
              draggable={false}
              onContextMenu={e => e.preventDefault()}
              className="h-full w-full object-cover"
            />
          </button>
          {renderOverlay?.(img, i)}
        </li>
      ))}
    </ul>
  )
}
