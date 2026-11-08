import { StatusBadge } from '@/components/StatusBadge'
import { formatDatetime } from '@/lib/format'
import type { PortalRequest } from '@/lib/portalTypes'
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
        </li>
      ))}
    </ul>
  )
}
