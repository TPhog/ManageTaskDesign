import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Check, CopyPlus, MessageSquareText, X } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { MoneyText } from '@/components/MoneyText'
import { QrCard } from '@/components/QrCard'
import { StatusBadge } from '@/components/StatusBadge'
import type { ApiError } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { QuotationDetail } from '@/lib/quotationTypes'
import { AcceptDialog, PaymentDialog, RejectDialog } from './QuotationActionDialogs'
import { clientStatementNotes, kindLabel, paymentActions, quotationErrorMessage, type PaymentAction } from './quotationUtils'
import { useQuotationCommand } from './useQuotations'
import { QuotationMessageDialog } from './QuotationMessageDialog'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

function TotalLine({ label, strong, testId, children }: { label: string; strong?: boolean; testId?: string; children: ReactNode }) {
  return (
    <div className={cn('flex items-baseline justify-between gap-3 border-b py-2 text-sm', strong && 'border-foreground')}>
      <dt className={strong ? 'font-semibold text-foreground' : 'text-muted-foreground'}>{label}</dt>
      <dd data-testid={testId} className={cn('tabular-nums text-foreground', strong && 'font-display text-display-md font-bold')}>{children}</dd>
    </div>
  )
}

/** Non-DRAFT quotation, rendered from its snapshot only (BR-39). */
export function QuotationView({ quotation: q }: { quotation: QuotationDetail }) {
  const navigate = useNavigate()
  const command = useQuotationCommand(q.id)
  const [dialog, setDialog] = useState<'accept' | 'reject' | null>(null)
  const [payment, setPayment] = useState<PaymentAction | null>(null)
  const location = useLocation()
  const [message, setMessage] = useState<'sent' | 'manual' | null>(null)

  // docs/08 §2.3 bước 6: the editor lands here with { justSent: true } after "Gửi"; clear it so a reload does not reopen
  useEffect(() => {
    if ((location.state as { justSent?: boolean } | null)?.justSent) {
      setMessage('sent')
      navigate('.', { replace: true, state: null })
    }
  }, [location.state, navigate])

  const s = q.snapshot
  if (!s) return null // DB CHECK: every non-DRAFT quotation has a snapshot

  const latestVersion = Math.max(...q.versions.map(v => v.version))
  const latest = q.versions.find(v => v.version === latestVersion)
  const draft = q.versions.find(v => v.status === 'DRAFT')
  const chainAccepted = q.versions.some(v => v.status === 'ACCEPTED')
  const canNewVersion = (q.status === 'SENT' || q.status === 'REJECTED') && q.version === latestVersion && !chainAccepted

  function newVersion() {
    command.mutate(
      { action: 'new-version', body: {} },
      {
        onSuccess: d => {
          toast.success(`Đã tạo version ${d.version} (nháp)`)
          navigate(`/admin/quotations/${d.id}`)
        },
        onError: err => toast.error(quotationErrorMessage(err as ApiError, 'Không thể tạo version mới')),
      },
    )
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <div className="space-y-6">
        <div className={`${panelClass} space-y-4`}>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="font-display text-[1.5rem] font-bold leading-[1.1] tracking-[-0.02em] text-foreground sm:text-[1.75rem]">{s.title}</h1>
            <StatusBadge status={q.effectiveStatus} />
            {q.status === 'ACCEPTED' && <StatusBadge status={q.paymentStatus} />}
          </div>
          <p className="text-sm text-muted-foreground">
            {s.number} · v{s.version} · {kindLabel[q.kind]} · Ngày báo giá {formatDate(s.issueDate)}
            {q.status === 'SENT' && ` · Hiệu lực đến ${formatDate(s.expiryDate)}`}
          </p>
          {clientStatementNotes(q).map(t => (
            <p key={t} role="status" className="rounded-lg bg-highlight-soft p-3 text-sm text-foreground">{t}</p>
          ))}
          {q.effectiveStatus === 'EXPIRED' && (
            <p className="rounded-lg border border-warning/30 bg-warning-soft p-3 text-sm text-warning">
              Báo giá đã quá hạn. Vẫn có thể đánh dấu chấp nhận nếu khách đồng ý muộn, hoặc tạo version mới để gia hạn.
            </p>
          )}
          {q.status === 'REJECTED' && q.rejectedReason && (
            <p className="text-sm text-foreground">Lý do từ chối: {q.rejectedReason}</p>
          )}
          {q.status === 'ACCEPTED' && (
            <p className="text-sm text-foreground">
              Khách chấp nhận ngày {formatDate(q.acceptedAt)}{q.acceptedNote ? ` — ${q.acceptedNote}` : ''}
            </p>
          )}
          {q.status === 'SUPERSEDED' && latest && latest.id !== q.id && (
            <p className="text-sm text-muted-foreground">
              Đã có version mới thay thế.{' '}
              <Link className="link-inline" to={`/admin/quotations/${latest.id}`}>Xem v{latest.version}</Link>
            </p>
          )}
          {draft && (
            <p className="text-sm text-muted-foreground">
              Đang soạn v{draft.version}.{' '}
              <Link className="link-inline" to={`/admin/quotations/${draft.id}`}>Mở bản nháp</Link>
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            {q.status === 'SENT' && (
              <>
                <Button onClick={() => setDialog('accept')}>
                  <Check aria-hidden="true" />
                  Đánh dấu đã chấp nhận
                </Button>
                <Button variant="outline" onClick={() => setDialog('reject')}>
                  <X aria-hidden="true" />
                  Từ chối
                </Button>
                <Button variant="outline" onClick={() => setMessage('manual')}>
                  <MessageSquareText aria-hidden="true" />
                  Tin nhắn mẫu
                </Button>
              </>
            )}
            {canNewVersion && (
              <Button variant="outline" disabled={command.isPending} onClick={newVersion}>
                <CopyPlus aria-hidden="true" />
                Tạo version mới
              </Button>
            )}
            {paymentActions(q).map(a => (
              <Button key={a.to} variant={a.undo ? 'ghost' : 'default'} onClick={() => setPayment(a)}>
                {a.label}
              </Button>
            ))}
          </div>
        </div>

        <div className={`${panelClass} space-y-6`}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <p className="kicker text-muted-foreground">Bên báo giá</p>
              <p className="mt-1 text-sm font-medium text-foreground">{s.designer.displayName}</p>
              <p className="text-sm text-muted-foreground">{[s.designer.phone, s.designer.email].filter(Boolean).join(' · ')}</p>
            </div>
            <div>
              <p className="kicker text-muted-foreground">Khách hàng</p>
              <p className="mt-1 text-sm font-medium text-foreground">{s.client.name}</p>
              {s.client.company && <p className="text-sm text-muted-foreground">{s.client.company}</p>}
            </div>
          </div>

          <div className="relative overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className={tableHeadRowClass}>
                  <th className="py-2.5 pr-3">Mô tả</th>
                  <th className="py-2.5 pr-3 text-right">SL</th>
                  <th className="py-2.5 pr-3 text-right">Đơn giá</th>
                  <th className="py-2.5 text-right">Thành tiền</th>
                </tr>
              </thead>
              <tbody>
                {s.items.map((it, i) => (
                  <tr key={`${i}-${it.description}`} className={tableRowClass}>
                    <td className="py-2.5 pr-3">
                      <p className="font-medium text-foreground">{it.description}</p>
                      {it.details && <p className="text-xs text-muted-foreground">{it.details}</p>}
                    </td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{it.quantity}</td>
                    <td className="py-2.5 pr-3 text-right"><MoneyText amount={it.unitPrice} /></td>
                    <td className="py-2.5 text-right"><MoneyText amount={it.amount} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <dl className="ml-auto w-full max-w-sm text-sm">
            <TotalLine label="Tạm tính"><MoneyText amount={s.subtotal} /></TotalLine>
            <TotalLine label="Giảm giá">− <MoneyText amount={s.discountAmount} /></TotalLine>
            <TotalLine label={`VAT ${s.vatPercent}%`}><MoneyText amount={s.vatAmount} /></TotalLine>
            <TotalLine label="Tổng cộng" strong testId="snapshot-total"><MoneyText amount={s.total} /></TotalLine>
            <TotalLine label={`Cọc ${s.depositPercent}%`}><MoneyText amount={s.depositAmount} /></TotalLine>
          </dl>

          {s.terms && (
            <div>
              <p className="kicker mb-1.5 text-muted-foreground">Điều khoản</p>
              <p className="whitespace-pre-wrap text-sm text-foreground">{s.terms}</p>
            </div>
          )}
          {s.notesToClient && (
            <div>
              <p className="kicker mb-1.5 text-muted-foreground">Ghi chú cho khách</p>
              <p className="whitespace-pre-wrap text-sm text-foreground">{s.notesToClient}</p>
            </div>
          )}
          {q.internalNotes && (
            <div>
              <p className="kicker mb-1.5 text-muted-foreground">Ghi chú nội bộ (khách không thấy)</p>
              <p className="whitespace-pre-wrap rounded-lg bg-muted p-3 text-sm text-foreground">{q.internalNotes}</p>
            </div>
          )}
        </div>
      </div>

      <aside className="space-y-4 lg:sticky lg:top-6 lg:self-start">
        {q.qrOptions.length > 0 ? (
          <QrCard options={q.qrOptions} bank={s.bank} pngUrl={purpose => `/api/admin/quotations/${q.id}/qr/${purpose}.png`} />
        ) : q.status === 'ACCEPTED' && q.paymentStatus === 'PAID' ? (
          <div className={panelClass}>
            <p className="font-semibold text-success">Đã thanh toán</p>
            <p className="mt-1 text-sm text-muted-foreground">Nhận đủ ngày {formatDate(q.paidAt)}</p>
          </div>
        ) : (
          <div className={panelClass}>
            <p className="text-sm text-muted-foreground">Không có mã QR ở trạng thái này.</p>
          </div>
        )}
        <div className={panelClass}>
          <h2 className="mb-3 font-display text-title font-bold text-foreground">Phiên bản</h2>
          <ul className="space-y-2">
            {q.versions.map(v => (
              <li key={v.id} className="flex items-center justify-between gap-2 text-sm">
                {v.id === q.id
                  ? <span className="font-medium text-foreground">v{v.version} (đang xem)</span>
                  : <Link className="link-inline" to={`/admin/quotations/${v.id}`}>v{v.version}</Link>}
                <StatusBadge status={v.effectiveStatus} />
              </li>
            ))}
          </ul>
        </div>
      </aside>

      <AcceptDialog quotation={q} open={dialog === 'accept'} onClose={() => setDialog(null)} />
      <RejectDialog quotation={q} open={dialog === 'reject'} onClose={() => setDialog(null)} />
      <PaymentDialog quotation={q} action={payment} onClose={() => setPayment(null)} />
      <QuotationMessageDialog
        quotation={q}
        open={message !== null}
        onOpenChange={open => { if (!open) setMessage(null) }}
        justSent={message === 'sent'}
      />
    </div>
  )
}
