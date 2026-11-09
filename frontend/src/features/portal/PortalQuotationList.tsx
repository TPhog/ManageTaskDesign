import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import type { PortalQuotationListItem } from '@/lib/portalTypes'
import { portalPaymentStatus } from './labels'
import { quotationBadgeLabel, showsPaymentStatus, versionText } from './quotation'

interface Props {
  items: PortalQuotationListItem[]
  /** project name on each row — for the cross-project list */
  showProject?: boolean
}

/** The client's quotations as ruled invoice rows; every figure comes from the snapshot (BR-39). */
export function PortalQuotationList({ items, showProject = false }: Props) {
  return (
    <ul className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
      {items.map(q => (
        <li key={q.id}>
          <Link
            to={`/portal/quotations/${q.id}`}
            className="group grid min-h-16 grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 py-3.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <div className="min-w-0">
              <p className="break-words font-display text-[1.0625rem] font-medium leading-snug text-foreground group-hover:underline group-hover:decoration-highlight group-hover:underline-offset-4">
                {q.title}
              </p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {versionText(q)}
                {showProject ? ` · ${q.projectName}` : ''}
              </p>
              <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-0.5 text-xs text-muted-foreground">
                <StatusBadge status={q.effectiveStatus} label={quotationBadgeLabel(q)} />
                {showsPaymentStatus(q) && <StatusBadge status={q.paymentStatus} label={portalPaymentStatus[q.paymentStatus]} />}
                {(q.effectiveStatus === 'SENT' || q.effectiveStatus === 'EXPIRED') && <span>Hiệu lực đến {formatDate(q.expiryDate)}</span>}
              </div>
            </div>
            <span className="flex items-center gap-1.5 whitespace-nowrap font-semibold tabular-nums text-foreground">
              <MoneyText amount={q.total} />
              <ChevronRight className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            </span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
