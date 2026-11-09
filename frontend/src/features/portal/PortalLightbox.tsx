import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import * as DialogPrimitive from '@radix-ui/react-dialog'
import { ChevronLeft, ChevronRight, X } from 'lucide-react'
import { Dialog, DialogDescription, DialogOverlay, DialogPortal, DialogTitle } from '@/components/ui/dialog'
import type { LightboxImage } from '@/lib/imageTypes'
import { DOUBLE_TAP_SCALE, clampScale, distance, isDoubleTap, pinchScale, swipeDirection, type Point } from './lightboxGestures'

interface Props { images: LightboxImage[]; index: number | null; onIndexChange: (index: number | null) => void }
interface View { scale: number; x: number; y: number }
const RESET: View = { scale: 1, x: 0, y: 0 }
const block = (e: { preventDefault: () => void }) => e.preventDefault()
const iconButton = 'flex h-12 w-12 items-center justify-center border border-on-inverse-muted/40 bg-surface-inverse-raised/80 text-on-inverse transition-colors hover:bg-surface-inverse-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-on-inverse-accent disabled:opacity-30'

/** CF-06: full-screen viewer for the client — pinch / double-tap zoom, swipe between images, 48px controls. */
export function PortalLightbox({ images, index, onIndexChange }: Props) {
  const at = index ?? -1
  const open = at >= 0 && at < images.length
  const current = open ? images[at] : null
  const [view, setView] = useState<View>(RESET)
  const [loaded, setLoaded] = useState(false)
  const [shownAt, setShownAt] = useState(at)
  const pointers = useRef(new Map<number, Point>())
  const start = useRef<{ p: Point; view: View; dist: number } | null>(null)
  const lastTap = useRef<{ t: number; p: Point } | null>(null)
  // A touch double tap is handled in onPointerUp; browsers then also fire a synthetic dblclick, which must not toggle again.
  const lastPointerType = useRef<string | null>(null)

  // Paging resets zoom and the "preview loaded" flag (state adjusted during render, not in an effect).
  if (shownAt !== at) {
    setShownAt(at)
    setView(RESET)
    setLoaded(false)
  }

  useEffect(() => {
    if (!open) return
    function onKey(e: KeyboardEvent) {
      if (e.key === 'ArrowRight' && at < images.length - 1) onIndexChange(at + 1)
      if (e.key === 'ArrowLeft' && at > 0) onIndexChange(at - 1)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, at, images.length, onIndexChange])

  function toggleZoom(p: Point, box: DOMRect) {
    setView(v => v.scale > 1 ? RESET : {
      scale: DOUBLE_TAP_SCALE,
      x: (box.left + box.width / 2 - p.x) * (DOUBLE_TAP_SCALE - 1),
      y: (box.top + box.height / 2 - p.y) * (DOUBLE_TAP_SCALE - 1),
    })
  }

  function onPointerDown(e: ReactPointerEvent<HTMLImageElement>) {
    lastPointerType.current = e.pointerType
    e.currentTarget.setPointerCapture?.(e.pointerId)
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
    const pts = [...pointers.current.values()]
    start.current = { p: { x: e.clientX, y: e.clientY }, view, dist: pts.length === 2 ? distance(pts[0], pts[1]) : 0 }
  }

  function onPointerMove(e: ReactPointerEvent<HTMLImageElement>) {
    if (!pointers.current.has(e.pointerId) || !start.current) return
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
    const pts = [...pointers.current.values()]
    const s = start.current
    if (pts.length === 2) setView(v => ({ ...v, scale: pinchScale(s.view.scale, s.dist, distance(pts[0], pts[1])) }))
    else if (view.scale > 1) setView(v => ({ ...v, x: s.view.x + e.clientX - s.p.x, y: s.view.y + e.clientY - s.p.y }))
  }

  function onPointerUp(e: ReactPointerEvent<HTMLImageElement>) {
    const s = start.current
    pointers.current.delete(e.pointerId)
    if (!s || pointers.current.size > 0) return
    const p = { x: e.clientX, y: e.clientY }
    if (view.scale <= 1) {
      const dir = swipeDirection(p.x - s.p.x, p.y - s.p.y)
      if (dir === 'next' && at < images.length - 1) onIndexChange(at + 1)
      if (dir === 'prev' && at > 0) onIndexChange(at - 1)
    }
    if (e.pointerType === 'touch') {
      if (isDoubleTap(lastTap.current, e.timeStamp, p)) { toggleZoom(p, e.currentTarget.getBoundingClientRect()); lastTap.current = null }
      else lastTap.current = { t: e.timeStamp, p }
    }
    setView(v => ({ ...v, scale: clampScale(v.scale) }))
    start.current = null
  }

  return (
    <Dialog open={open} onOpenChange={o => { if (!o) onIndexChange(null) }}>
      <DialogPortal>
        <DialogOverlay className="bg-surface-inverse backdrop-blur-none" />
        <DialogPrimitive.Content
          className="fixed inset-0 z-50 flex h-[100dvh] w-screen flex-col bg-surface-inverse text-on-inverse outline-none"
          onContextMenu={block}
        >
          <DialogTitle className="sr-only">{current?.alt ?? 'Ảnh'}</DialogTitle>
          <DialogDescription className="sr-only">Ảnh {open ? at + 1 : 0} trên {images.length}. Chạm hai lần hoặc dùng hai ngón để phóng to.</DialogDescription>
          <div className="flex items-center justify-between gap-2 border-b border-on-inverse-muted/30 p-2 pt-[max(0.5rem,env(safe-area-inset-top))]">
            <p className="min-w-0 truncate px-2 font-serif text-sm italic text-on-inverse-muted">
              {current?.caption ? `${current.caption} · ` : ''}{at + 1} / {images.length}
            </p>
            <DialogPrimitive.Close className={iconButton} aria-label="Đóng"><X className="h-6 w-6" aria-hidden="true" /></DialogPrimitive.Close>
          </div>
          {current && (
            <div className="relative flex flex-1 items-center justify-center overflow-hidden">
              {!loaded && <img src={current.thumbSrc} alt="" aria-hidden="true" className="absolute max-h-full max-w-full object-contain blur-sm" />}
              <img
                src={current.src}
                alt={current.alt}
                draggable={false}
                onLoad={() => setLoaded(true)}
                onContextMenu={block}
                onDragStart={block}
                onDoubleClick={e => {
                  if (lastPointerType.current === 'touch') return
                  toggleZoom({ x: e.clientX, y: e.clientY }, e.currentTarget.getBoundingClientRect())
                }}
                onPointerDown={onPointerDown}
                onPointerMove={onPointerMove}
                onPointerUp={onPointerUp}
                onPointerCancel={onPointerUp}
                style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.scale})`, touchAction: 'none' }}
                className="relative max-h-full max-w-full select-none object-contain transition-transform duration-150 motion-reduce:transition-none"
              />
            </div>
          )}
          {images.length > 1 && (
            <div className="flex items-center justify-between gap-2 border-t border-on-inverse-muted/30 p-2 pb-[max(0.5rem,env(safe-area-inset-bottom))]">
              <button type="button" aria-label="Ảnh trước" disabled={at === 0} onClick={() => onIndexChange(at - 1)} className={iconButton}>
                <ChevronLeft className="h-6 w-6" aria-hidden="true" />
              </button>
              <p className="text-center text-xs text-on-inverse-muted">Vuốt sang trái/phải để xem ảnh khác</p>
              <button type="button" aria-label="Ảnh sau" disabled={at === images.length - 1} onClick={() => onIndexChange(at + 1)} className={iconButton}>
                <ChevronRight className="h-6 w-6" aria-hidden="true" />
              </button>
            </div>
          )}
        </DialogPrimitive.Content>
      </DialogPortal>
    </Dialog>
  )
}
