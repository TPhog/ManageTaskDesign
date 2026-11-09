import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, SearchX } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { EditorialSection, Kicker, LeaderList, LeaderRow, LeaderTotal, PullQuote } from './kit/editorial'
import { portalPaymentStatus, portalQuotationKind, portalQuotationStatus } from './labels'
import { PortalPaymentBlock } from './PortalPaymentBlock'
import { QuotationDecision } from './QuotationDecision'
import { quotationBadgeLabel, quotationNotice, showsPaymentStatus, versionText, type NoticeTone } from './quotation'
import { usePortalMe, usePortalQuotation } from './usePortal'

const noticeRule: Record<NoticeTone, string> = {
  info: 'border-foreground',
  warning: 'border-warning',
  success: 'border-success',
  muted: 'border-border',
}

const ROMAN = ['I.', 'II.', 'III.', 'IV.', 'V.', 'VI.', 'VII.']

/** docs/08 §3.2 — rendered from the immutable snapshot only (BR-39); the client records "Đồng ý" / "Đã chuyển" (ADR-012). */
export default function PortalQuotationPage() {
  const { id = '', version } = useParams<{ id: string; version?: string }>()
  const versionNo = version ? Number(version) : undefined
  const { data: q, isLoading, error } = usePortalQuotation(id, versionNo)
  const { data: me } = usePortalMe()

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-36 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (error || !q) {
    return (
      <div className="border-t-[3px] border-rule">
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy báo giá"
          description="Báo giá không tồn tại hoặc không thuộc tài khoản của bạn."
          action={
            <Button asChild variant="outline" className="h-11">
              <Link to="/portal/quotations">Về danh sách báo giá</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const s = q.snapshot
  const live = !versionNo
  const designerName = s.designer.displayName || me?.designer.displayName || ''
  // The live page's QuotationDecision already says "Bạn đã đồng ý lúc … — đang chờ" with the time; don't say it twice.
  const agreedShownByDecision = live && q.status === 'SENT' && !!q.clientAcceptedAt
  const notice = agreedShownByDecision ? null : quotationNotice(q, designerName)
  const showCompany = !!s.client.company && s.client.company.trim().toLowerCase() !== s.client.name.trim().toLowerCase()
  const hasPayment = live && q.qrOptions.length > 0
  const chapters = [
    'items', hasPayment && 'payment', s.notesToClient.trim() && 'notes', s.terms.trim() && 'terms', 'info',
    q.versions.length > 1 && 'versions',
  ].filter((c): c is string => !!c)
  const no = (key: string) => ROMAN[chapters.indexOf(key)]

  return (
    <div className="space-y-6">
      <Link to="/portal/quotations" className="-ml-1 inline-flex min-h-11 items-center gap-1 px-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Tất cả báo giá
      </Link>

      <header className="-mt-2">
        <Kicker tone="highlight">{portalQuotationKind[s.kind] ?? s.kind} · {versionText(s)}</Kicker>
        <h1 className="mt-2 font-display text-[2rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">{s.title}</h1>
        {s.project.name.trim() !== s.title.trim() && (
          <p className="mt-1 font-serif text-base italic text-muted-foreground">{s.project.name}</p>
        )}
        <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1">
          <StatusBadge status={q.effectiveStatus} label={quotationBadgeLabel(q)} />
          {showsPaymentStatus(q) && <StatusBadge status={q.paymentStatus} label={portalPaymentStatus[q.paymentStatus]} />}
        </div>
        <dl className="mt-4 grid grid-cols-2 border-y border-border">
          <div className="py-3 pr-4">
            <dt className="text-xs text-muted-foreground">Ngày báo giá</dt>
            <dd className="m-0 mt-0.5 font-display text-lg font-medium text-foreground">{formatDate(s.issueDate)}</dd>
          </div>
          <div className="border-l border-border py-3 pl-4">
            <dt className="text-xs text-muted-foreground">Hiệu lực đến</dt>
            <dd className="m-0 mt-0.5 font-display text-lg font-medium text-foreground">{formatDate(s.expiryDate)}</dd>
          </div>
        </dl>
      </header>

      {notice && (
        <p role="status" className={cn('border-l-4 py-1 pl-4 font-serif text-base italic leading-relaxed text-foreground', noticeRule[notice.tone])}>
          {notice.text}
        </p>
      )}

      {live && <QuotationDecision quotation={q} />}

      <EditorialSection number={no('items')} title="Nội dung báo giá">
        <ol className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
          {s.items.map((item, i) => (
            <li key={i} className="flex items-start justify-between gap-3 py-3">
              <div className="min-w-0">
                <p className="font-display text-[1.0625rem] font-medium leading-snug text-foreground">{item.description}</p>
                {item.details && <p className="mt-0.5 whitespace-pre-wrap break-words text-xs text-muted-foreground">{item.details}</p>}
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {item.quantity} × <MoneyText amount={item.unitPrice} />
                </p>
              </div>
              <span className="whitespace-nowrap text-sm font-semibold text-foreground">
                <MoneyText amount={item.amount} />
              </span>
            </li>
          ))}
        </ol>
        <LeaderList className="mt-1">
          <LeaderRow label="Tạm tính" value={<MoneyText amount={s.subtotal} />} />
          {s.discountAmount > 0 && <LeaderRow label="Giảm giá" value={<>−<MoneyText amount={s.discountAmount} /></>} />}
          {s.vatPercent > 0 && <LeaderRow label={`VAT ${s.vatPercent}%`} value={<MoneyText amount={s.vatAmount} />} />}
          <LeaderTotal label="Tổng cộng" value={<span data-testid="portal-total"><MoneyText amount={s.total} /></span>} />
        </LeaderList>
        {s.depositPercent > 0 && (
          <LeaderList>
            <LeaderRow tone="muted" label={`Tiền cọc (${s.depositPercent}%)`} value={<MoneyText amount={s.depositAmount} />} />
          </LeaderList>
        )}
      </EditorialSection>

      {hasPayment && <PortalPaymentBlock quotation={q} number={no('payment')} />}

      {s.notesToClient.trim() && (
        <EditorialSection number={no('notes')} title="Lời nhắn của nhà thiết kế">
          <div className="mt-2 whitespace-pre-wrap break-words">
            <PullQuote cite={designerName || undefined}>{s.notesToClient}</PullQuote>
          </div>
        </EditorialSection>
      )}

      {s.terms.trim() && (
        <EditorialSection number={no('terms')} title="Điều khoản">
          <p className="mt-2 max-w-[60ch] whitespace-pre-wrap break-words text-sm leading-relaxed text-foreground">{s.terms}</p>
        </EditorialSection>
      )}

      <EditorialSection number={no('info')} title="Thông tin">
        <dl className="mt-2 grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="kicker text-muted-foreground">Nhà thiết kế</dt>
            <dd className="m-0 mt-1 break-words text-foreground">
              <span className="font-display text-base font-medium">{s.designer.displayName || '—'}</span>
              {s.designer.phone && <span className="block text-muted-foreground">{s.designer.phone}</span>}
              {s.designer.email && <span className="block text-muted-foreground">{s.designer.email}</span>}
            </dd>
          </div>
          <div>
            <dt className="kicker text-muted-foreground">Khách hàng</dt>
            <dd className="m-0 mt-1 break-words text-foreground">
              <span className="font-display text-base font-medium">{s.client.name}</span>
              {showCompany && <span className="block text-muted-foreground">{s.client.company}</span>}
            </dd>
          </div>
        </dl>
      </EditorialSection>

      {q.versions.length > 1 && (
        <EditorialSection number={no('versions')} title="Các phiên bản">
          <ul className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
            {q.versions.map(v => (
              <li key={v.version} className="flex min-h-11 items-center justify-between gap-3 py-1.5">
                {v.version === s.version ? (
                  <span className="text-sm font-semibold text-foreground">Phiên bản {v.version} (đang xem)</span>
                ) : (
                  <Link to={`/portal/quotations/${id}/versions/${v.version}`} className="link-inline inline-flex min-h-11 items-center text-sm">
                    Phiên bản {v.version}
                  </Link>
                )}
                <StatusBadge status={v.effectiveStatus} label={portalQuotationStatus[v.effectiveStatus]} />
              </li>
            ))}
          </ul>
        </EditorialSection>
      )}
    </div>
  )
}
