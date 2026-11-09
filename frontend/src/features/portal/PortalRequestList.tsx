import { useState } from 'react'
import { ImageGrid } from '@/components/ImageGrid'
import { formatDatetime } from '@/lib/format'
import type { LightboxImage } from '@/lib/imageTypes'
import type { PortalRequest, PortalRequestAttachment, PortalRequestStatus } from '@/lib/portalTypes'
import { StatusText, type StatusTextProps } from './kit/editorial'
import { portalRequestStatus } from './labels'
import { PortalLightbox } from './PortalLightbox'

interface Props {
  items: PortalRequest[]
  /** project name on each row — for the cross-project list */
  showProject?: boolean
}

const statusTone: Record<PortalRequestStatus, StatusTextProps['tone']> = { NEW: 'highlight', TRIAGED: 'success', DECLINED: 'muted' }

/** REQ-06: the client's requests with status and, when declined, the designer's reason (BR-52). */
export function PortalRequestList({ items, showProject = false }: Props) {
  return (
    <ul className="border-b border-border [&>li+li]:border-t [&>li+li]:border-border">
      {items.map(r => (
        <li key={r.id} className="py-3.5">
          <p className="text-xs text-muted-foreground">
            {[showProject ? r.projectName : null, r.deliverableName, formatDatetime(r.createdAt)]
              .filter(Boolean)
              .join(' · ')}
          </p>
          <p className="mt-1 whitespace-pre-wrap break-words font-serif text-base leading-relaxed text-foreground">{r.content}</p>
          <div className="mt-1.5">
            <StatusText tone={statusTone[r.status] ?? 'default'}>{portalRequestStatus[r.status]}</StatusText>
          </div>
          {r.status === 'DECLINED' && r.declineReason && (
            <p className="mt-2 whitespace-pre-wrap break-words border-l-2 border-danger pl-3 text-sm text-foreground">Lý do: {r.declineReason}</p>
          )}
          <RequestPhotos attachments={r.attachments ?? []} />
        </li>
      ))}
    </ul>
  )
}

/** The client's own photos on a request (preview/thumb only) — tiles ≥ 100px at 390px (CF-12/13). */
function RequestPhotos({ attachments }: { attachments: PortalRequestAttachment[] }) {
  const [open, setOpen] = useState<number | null>(null)
  const images: LightboxImage[] = attachments
    .filter(a => a.thumbUrl && a.previewUrl)
    .map(a => ({ id: a.id, src: a.previewUrl!, thumbSrc: a.thumbUrl!, alt: a.originalName }))
  if (images.length === 0) return null
  return (
    <div className="mt-2.5">
      <ImageGrid images={images} onOpen={setOpen} className="grid-cols-3 gap-1.5 sm:grid-cols-4" />
      <PortalLightbox images={images} index={open} onIndexChange={setOpen} />
    </div>
  )
}
