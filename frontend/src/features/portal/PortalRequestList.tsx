import { useState } from 'react'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDatetime } from '@/lib/format'
import type { LightboxImage } from '@/lib/imageTypes'
import type { PortalRequest, PortalRequestAttachment } from '@/lib/portalTypes'
import { portalRequestStatus } from './labels'

interface Props {
  items: PortalRequest[]
  /** project name on each row — for the cross-project list */
  showProject?: boolean
}

/** REQ-06: the client's requests with status and, when declined, the designer's reason (BR-52). */
export function PortalRequestList({ items, showProject = false }: Props) {
  return (
    <ul className="divide-y">
      {items.map(r => (
        <li key={r.id} className="py-3 first:pt-0 last:pb-0">
          <div className="flex items-start justify-between gap-3">
            <p className="min-w-0 whitespace-pre-wrap break-words text-sm text-foreground">{r.content}</p>
            <StatusBadge status={r.status} label={portalRequestStatus[r.status]} />
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            {[showProject ? r.projectName : null, r.deliverableName, formatDatetime(r.createdAt)]
              .filter(Boolean)
              .join(' · ')}
          </p>
          {r.status === 'DECLINED' && r.declineReason && (
            <p className="mt-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">Lý do: {r.declineReason}</p>
          )}
          <RequestPhotos attachments={r.attachments ?? []} />
        </li>
      ))}
    </ul>
  )
}

/** The client's own photos on a request (preview/thumb only). */
function RequestPhotos({ attachments }: { attachments: PortalRequestAttachment[] }) {
  const [open, setOpen] = useState<number | null>(null)
  const images: LightboxImage[] = attachments
    .filter(a => a.thumbUrl && a.previewUrl)
    .map(a => ({ id: a.id, src: a.previewUrl!, thumbSrc: a.thumbUrl!, alt: a.originalName }))
  if (images.length === 0) return null
  return (
    <div className="mt-2">
      <ImageGrid images={images} onOpen={setOpen} className="grid-cols-5" />
      <ImageLightbox images={images} index={open} onIndexChange={setOpen} />
    </div>
  )
}
