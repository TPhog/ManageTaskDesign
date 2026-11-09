import { useEffect } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import type { LightboxImage } from '@/lib/imageTypes'

interface Props {
  images: LightboxImage[]
  /** null = closed */
  index: number | null
  onIndexChange: (index: number | null) => void
}

const block = (e: { preventDefault: () => void }) => e.preventDefault()

/**
 * Full-screen viewer (spec §7). Right-click and drag are blocked as a deterrent only — the watermark on the
 * preview is the real protection.
 */
export function ImageLightbox({ images, index, onIndexChange }: Props) {
  const at = index ?? -1
  const open = at >= 0 && at < images.length
  const current = open ? images[at] : null

  useEffect(() => {
    if (!open) return
    function onKey(e: KeyboardEvent) {
      if (e.key === 'ArrowRight' && at < images.length - 1) onIndexChange(at + 1)
      if (e.key === 'ArrowLeft' && at > 0) onIndexChange(at - 1)
    }
    // Esc closes only the viewer, never a drawer/sheet underneath it: swallow it before Radix's document listeners
    function onEscape(e: KeyboardEvent) {
      if (e.key !== 'Escape') return
      e.stopImmediatePropagation()
      onIndexChange(null)
    }
    window.addEventListener('keydown', onKey)
    window.addEventListener('keydown', onEscape, true)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('keydown', onEscape, true)
    }
  }, [open, at, images.length, onIndexChange])

  return (
    <Dialog open={open} onOpenChange={o => { if (!o) onIndexChange(null) }}>
      <DialogContent
        className="tone-inverse max-w-[min(96vw,1400px)] border-none bg-background p-2 text-foreground sm:p-4"
        onContextMenu={block}
      >
        <DialogTitle className="sr-only">{current?.alt ?? 'Ảnh'}</DialogTitle>
        <DialogDescription className="sr-only">
          Ảnh {open ? at + 1 : 0} trên {images.length}
        </DialogDescription>
        {current && (
          <figure className="relative flex flex-col items-center gap-2">
            <img
              src={current.src}
              alt={current.alt}
              draggable={false}
              onContextMenu={block}
              onDragStart={block}
              className="max-h-[82vh] w-auto select-none object-contain"
            />
            {images.length > 1 && (
              <>
                <button
                  type="button"
                  aria-label="Ảnh trước"
                  disabled={at === 0}
                  onClick={() => onIndexChange(at - 1)}
                  className="absolute left-1 top-1/2 -translate-y-1/2 rounded-full bg-card/80 p-2 text-foreground hover:bg-card disabled:opacity-30"
                >
                  <ChevronLeft className="h-5 w-5" aria-hidden="true" />
                </button>
                <button
                  type="button"
                  aria-label="Ảnh sau"
                  disabled={at === images.length - 1}
                  onClick={() => onIndexChange(at + 1)}
                  className="absolute right-1 top-1/2 -translate-y-1/2 rounded-full bg-card/80 p-2 text-foreground hover:bg-card disabled:opacity-30"
                >
                  <ChevronRight className="h-5 w-5" aria-hidden="true" />
                </button>
              </>
            )}
            <figcaption className="text-center text-xs text-muted-foreground">
              {current.caption ? `${current.caption} · ` : ''}{at + 1} / {images.length}
            </figcaption>
          </figure>
        )}
      </DialogContent>
    </Dialog>
  )
}
