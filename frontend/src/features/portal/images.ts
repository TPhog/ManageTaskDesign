import type { LightboxImage, PortalImage } from '@/lib/imageTypes'

/** The client always opens the watermarked preview (spec §4); the grid uses the thumb. */
export function toLightbox(images: PortalImage[]): LightboxImage[] {
  return images.map(i => ({
    id: i.id,
    src: i.previewUrl,
    thumbSrc: i.thumbUrl,
    alt: i.deliverableName ? `Ảnh ${i.deliverableName}` : 'Ảnh tiến độ',
    caption: i.deliverableName ?? undefined,
  }))
}

/** "Hình ảnh" section: one group per deliverable (first-seen order), then progress photos, then the rest. */
export function groupPortalImages(images: PortalImage[]): { key: string; title: string; images: LightboxImage[] }[] {
  const byDeliverable = new Map<string, { title: string; items: PortalImage[] }>()
  const progress: PortalImage[] = []
  const other: PortalImage[] = []
  for (const i of images) {
    if (i.deliverableId && i.deliverableName) {
      const g = byDeliverable.get(i.deliverableId) ?? { title: i.deliverableName, items: [] }
      g.items.push(i)
      byDeliverable.set(i.deliverableId, g)
    } else if (i.source === 'PROGRESS_UPDATE') {
      progress.push(i)
    } else {
      other.push(i)
    }
  }
  const groups = [...byDeliverable.entries()].map(([key, g]) => ({ key, title: g.title, images: toLightbox(g.items) }))
  if (progress.length > 0) groups.push({ key: 'progress', title: 'Cập nhật tiến độ', images: toLightbox(progress) })
  if (other.length > 0) groups.push({ key: 'other', title: 'Ảnh khác', images: toLightbox(other) })
  return groups
}
