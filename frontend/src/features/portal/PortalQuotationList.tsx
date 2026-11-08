import { Link } from 'react-router-dom'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import type { PortalQuotationListItem } from '@/lib/portalTypes'
import { portalPaymentStatus, portalQuotationStatus } from './labels'
import { showsPaymentStatus, versionText } from './quotation'

interface Props {
  items: PortalQuotationListItem[]
  /** project name on each card — for the cross-project list */
  showProject?: boolean
}

/** The client's quotations; every figure comes from the snapshot (BR-39). */
export function PortalQuotationList({ items, showProject = false }: Props) {
  return (
    <ul className="space-y-3">
      {items.map(q => (
        <li key={q.id}>
          <Link
            to={`/portal/quotations/${q.id}`}
            className="block rounded-xl border bg-card p-4 shadow-card transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate font-semibold text-foreground">{q.title}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {versionText(q)}
                  {showProject ? ` · ${q.projectName}` : ''}
                </p>
              </div>
              <span className="whitespace-nowrap font-semibold text-foreground">
                <MoneyText amount={q.total} />
              </span>
            </div>
            <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              <StatusBadge status={q.effectiveStatus} label={portalQuotationStatus[q.effectiveStatus]} />
              {showsPaymentStatus(q) && (
                <StatusBadge status={q.paymentStatus} label={portalPaymentStatus[q.paymentStatus]} />
              )}
              {(q.effectiveStatus === 'SENT' || q.effectiveStatus === 'EXPIRED') && (
                <span>Hiệu lực đến {formatDate(q.expiryDate)}</span>
              )}
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}
