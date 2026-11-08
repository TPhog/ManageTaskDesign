import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, SearchX } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { QrCard } from '@/components/QrCard'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { portalPaymentStatus, portalQuotationKind, portalQuotationStatus } from './labels'
import { afterPaymentHint, qrPngUrl, quotationNotice, showsPaymentStatus, versionText, type NoticeTone } from './quotation'
import { usePortalMe, usePortalQuotation } from './usePortal'

const panelClass = 'rounded-xl border bg-card p-4 shadow-card sm:p-5'

const noticeClass: Record<NoticeTone, string> = {
  info: 'bg-blue-50 text-blue-800',
  warning: 'bg-amber-50 text-amber-800',
  success: 'bg-emerald-50 text-emerald-800',
  muted: 'bg-slate-100 text-slate-700',
}

interface RowProps {
  label: string
  children: ReactNode
  strong?: boolean
  testId?: string
}

function Row({ label, children, strong = false, testId }: RowProps) {
  return (
    <div className={cn('flex items-center justify-between gap-3', strong && 'text-base font-semibold')}>
      <dt className={strong ? 'text-foreground' : 'text-muted-foreground'}>{label}</dt>
      <dd data-testid={testId} className="text-right text-foreground">{children}</dd>
    </div>
  )
}

/** docs/08 §3.2 — rendered from the immutable snapshot only (BR-39); no accept/reject button (ADR-004). */
export default function PortalQuotationPage() {
  const { id = '', version } = useParams<{ id: string; version?: string }>()
  const versionNo = version ? Number(version) : undefined
  const { data: q, isLoading, error } = usePortalQuotation(id, versionNo)
  const { data: me } = usePortalMe()

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-36 w-full rounded-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }

  if (error || !q) {
    return (
      <div className="rounded-xl border bg-card shadow-card">
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy báo giá"
          description="Báo giá không tồn tại hoặc không thuộc tài khoản của bạn."
          action={
            <Button asChild variant="outline">
              <Link to="/portal/quotations">Về danh sách báo giá</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const s = q.snapshot
  const designerName = s.designer.displayName || me?.designer.displayName || ''
  const notice = quotationNotice(q, designerName)

  return (
    <div className="space-y-4">
      <Link to="/portal/quotations" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Tất cả báo giá
      </Link>

      <div className={panelClass}>
        <p className="text-xs font-medium text-muted-foreground">
          {portalQuotationKind[s.kind] ?? s.kind} · {versionText(s)}
        </p>
        <h1 className="mt-1 text-xl font-semibold tracking-tight text-foreground">{s.title}</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">{s.project.name}</p>
        <div className="mt-3 flex flex-wrap gap-2">
          <StatusBadge status={q.effectiveStatus} label={portalQuotationStatus[q.effectiveStatus]} />
          {showsPaymentStatus(q) && (
            <StatusBadge status={q.paymentStatus} label={portalPaymentStatus[q.paymentStatus]} />
          )}
        </div>
        <dl className="mt-4 grid grid-cols-2 gap-3">
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Ngày báo giá</dt>
            <dd className="mt-0.5 text-sm font-medium text-foreground">{formatDate(s.issueDate)}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Hiệu lực đến</dt>
            <dd className="mt-0.5 text-sm font-medium text-foreground">{formatDate(s.expiryDate)}</dd>
          </div>
        </dl>
      </div>

      {notice && (
        <p role="status" className={cn('rounded-lg px-3 py-2 text-sm', noticeClass[notice.tone])}>
          {notice.text}
        </p>
      )}

      <section aria-labelledby="quote-items" className={panelClass}>
        <h2 id="quote-items" className="text-sm font-semibold text-foreground">Nội dung báo giá</h2>
        <ul className="mt-3 divide-y">
          {s.items.map((item, i) => (
            <li key={i} className="flex items-start justify-between gap-3 py-3 first:pt-0">
              <div className="min-w-0">
                <p className="font-medium text-foreground">{item.description}</p>
                {item.details && (
                  <p className="mt-0.5 whitespace-pre-wrap text-xs text-muted-foreground">{item.details}</p>
                )}
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {item.quantity} × <MoneyText amount={item.unitPrice} />
                </p>
              </div>
              <span className="whitespace-nowrap text-sm font-medium text-foreground">
                <MoneyText amount={item.amount} />
              </span>
            </li>
          ))}
        </ul>
        <dl className="mt-3 space-y-2 border-t pt-3 text-sm">
          <Row label="Tạm tính"><MoneyText amount={s.subtotal} /></Row>
          {s.discountAmount > 0 && <Row label="Giảm giá">−<MoneyText amount={s.discountAmount} /></Row>}
          {s.vatPercent > 0 && <Row label={`VAT ${s.vatPercent}%`}><MoneyText amount={s.vatAmount} /></Row>}
          <Row label="Tổng cộng" strong testId="portal-total"><MoneyText amount={s.total} /></Row>
          {s.depositPercent > 0 && (
            <Row label={`Đặt cọc ${s.depositPercent}%`}><MoneyText amount={s.depositAmount} /></Row>
          )}
        </dl>
      </section>

      {q.qrOptions.length > 0 && (
        <div className="space-y-2">
          <QrCard options={q.qrOptions} bank={s.bank} pngUrl={qrPngUrl(q.id)} />
          <p className="px-1 text-sm text-muted-foreground">{afterPaymentHint(designerName)}</p>
        </div>
      )}

      {(s.notesToClient || s.terms) && (
        <section aria-labelledby="quote-terms" className={panelClass}>
          <h2 id="quote-terms" className="text-sm font-semibold text-foreground">Ghi chú &amp; điều khoản</h2>
          {s.notesToClient && <p className="mt-2 whitespace-pre-wrap text-sm text-foreground">{s.notesToClient}</p>}
          {s.terms && <p className="mt-2 whitespace-pre-wrap text-sm text-muted-foreground">{s.terms}</p>}
        </section>
      )}

      <section aria-labelledby="quote-parties" className={panelClass}>
        <h2 id="quote-parties" className="text-sm font-semibold text-foreground">Thông tin</h2>
        <dl className="mt-3 grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Designer</dt>
            <dd className="mt-0.5 text-foreground">
              {s.designer.displayName || '—'}
              {s.designer.phone && <span className="block text-muted-foreground">{s.designer.phone}</span>}
              {s.designer.email && <span className="block text-muted-foreground">{s.designer.email}</span>}
            </dd>
          </div>
          <div>
            <dt className="text-xs font-medium text-muted-foreground">Khách hàng</dt>
            <dd className="mt-0.5 text-foreground">
              {s.client.name}
              {s.client.company && <span className="block text-muted-foreground">{s.client.company}</span>}
            </dd>
          </div>
        </dl>
      </section>

      {q.versions.length > 1 && (
        <section aria-labelledby="quote-versions" className={panelClass}>
          <h2 id="quote-versions" className="text-sm font-semibold text-foreground">Các phiên bản</h2>
          <ul className="mt-2 divide-y">
            {q.versions.map(v => (
              <li key={v.version} className="flex items-center justify-between gap-3 py-2">
                {v.version === s.version ? (
                  <span className="text-sm font-medium text-foreground">Phiên bản {v.version} (đang xem)</span>
                ) : (
                  <Link
                    to={`/portal/quotations/${id}/versions/${v.version}`}
                    className="text-sm font-medium text-primary hover:underline"
                  >
                    Phiên bản {v.version}
                  </Link>
                )}
                <StatusBadge status={v.effectiveStatus} label={portalQuotationStatus[v.effectiveStatus]} />
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
