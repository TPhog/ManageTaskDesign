import { useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Copy, Download } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { MoneyText } from '@/components/MoneyText'
import { formatVnd } from '@/lib/format'
import type { PortalQuotationDetail } from '@/lib/portalTypes'
import type { QrPurpose } from '@/lib/quotationTypes'
import { cn } from '@/lib/utils'
import { EditorialSection } from './kit/editorial'
import { memoExplanation, noticeText, paymentSummary, qrPngUrl } from './quotation'
import { usePaymentNotice } from './useQuotationStatements'

const optionText: Record<QrPurpose, string> = {
  deposit: 'Chuyển tiền cọc (khuyên dùng)',
  full: 'Chuyển toàn bộ một lần',
  remaining: 'Chuyển phần còn lại',
}

async function copy(text: string, what: string) {
  try {
    await navigator.clipboard.writeText(text)
    toast.success(`Đã sao chép ${what}`)
  } catch {
    toast.error('Không sao chép được — hãy chọn và sao chép thủ công')
  }
}

/** One "NGÂN HÀNG · Vietcombank · [copy]" row — 52px tall, 44px copy button (CF-04). */
function CopyRow({ label, what, value, copyText, testId }: { label: string; what: string; value: ReactNode; copyText: string; testId?: string }) {
  return (
    <div className="flex min-h-13 items-center gap-3 border-b border-border py-1">
      <dt className="kicker w-28 shrink-0 text-muted-foreground">{label}</dt>
      <dd data-testid={testId} className="m-0 min-w-0 flex-1 break-words font-semibold tabular-nums text-foreground">{value}</dd>
      <Button
        type="button"
        variant="outline"
        size="icon"
        className="h-11 w-11 shrink-0"
        aria-label={`Sao chép ${what}`}
        onClick={() => void copy(copyText, what)}
      >
        <Copy aria-hidden="true" />
      </Button>
    </div>
  )
}

interface Props {
  quotation: PortalQuotationDetail
  /** chapter numeral of the section ("II.") */
  number?: string
}

/** CF-04/05: what to pay now and later, a selectable amount, the VietQR, copyable bank details and "Tôi đã chuyển khoản". */
export function PortalPaymentBlock({ quotation: q, number }: Props) {
  const [selected, setSelected] = useState<QrPurpose | null>(null)
  const [reporting, setReporting] = useState(false)
  const [note, setNote] = useState('')
  const notice = usePaymentNotice(q.id)
  const radios = useRef<(HTMLButtonElement | null)[]>([])
  const current = q.qrOptions.find(o => o.purpose === selected) ?? q.qrOptions[0]
  if (!current) return null

  // Review M-2: an expired quotation never asks for money — no amount due, no QR, no "Tôi đã chuyển khoản"
  if (q.effectiveStatus === 'EXPIRED') {
    return (
      <EditorialSection number={number} title="Thanh toán">
        <p role="status" className="mt-2 border-l-4 border-warning py-1 pl-4 font-serif text-base italic leading-relaxed text-foreground">
          Báo giá đã hết hạn, vui lòng liên hệ nhà thiết kế trước khi chuyển tiền.
        </p>
      </EditorialSection>
    )
  }

  /** ARIA radio pattern: one Tab stop, arrow keys move and select (review M-4). */
  function onRadioKey(e: KeyboardEvent<HTMLButtonElement>, i: number) {
    const step = e.key === 'ArrowDown' || e.key === 'ArrowRight' ? 1 : e.key === 'ArrowUp' || e.key === 'ArrowLeft' ? -1 : 0
    if (!step) return
    e.preventDefault()
    const next = (i + step + q.qrOptions.length) % q.qrOptions.length
    setSelected(q.qrOptions[next].purpose)
    radios.current[next]?.focus()
  }

  const sum = paymentSummary(q)
  const bank = q.snapshot.bank
  const pending = q.clientPaymentNotice?.pending ? q.clientPaymentNotice : null

  function report() {
    notice.mutate(
      { purpose: current.purpose, note: note.trim() || null },
      { onSuccess: () => { setReporting(false); setNote('') } },
    )
  }

  return (
    <EditorialSection number={number} title="Thanh toán">
      {sum && (
        <div className="mt-1">
          <div data-testid="pay-now" className="flex flex-wrap items-baseline justify-between gap-x-3 border-b-[3px] border-double border-rule py-3.5">
            <span className="font-semibold">{q.paymentStatus === 'DEPOSIT_PAID' ? 'Còn cần chuyển (phần còn lại)' : 'Bây giờ cần chuyển'}</span>
            <span className="font-display text-[2rem] font-medium leading-tight tracking-[-0.02em] tabular-nums">
              <MoneyText amount={sum.now} />
            </span>
          </div>
          {sum.later > 0 && (
            <p data-testid="pay-later" className="mt-2 font-serif text-sm italic text-muted-foreground">
              Còn lại <MoneyText amount={sum.later} /> sau khi bàn giao
            </p>
          )}
        </div>
      )}

      {q.qrOptions.length > 1 && (
        <div role="radiogroup" aria-label="Chọn khoản muốn chuyển" className="mt-5 space-y-2">
          {q.qrOptions.map((o, i) => {
            const on = o.purpose === current.purpose
            return (
              <button
                key={o.purpose}
                ref={el => { radios.current[i] = el }}
                type="button"
                role="radio"
                aria-checked={on}
                tabIndex={on ? 0 : -1}
                onKeyDown={e => onRadioKey(e, i)}
                onClick={() => setSelected(o.purpose)}
                className={cn(
                  'flex min-h-11 w-full items-center gap-3 border px-4 py-3 text-left text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  on ? 'border-foreground bg-card shadow-[inset_0_0_0_1px_var(--foreground)]' : 'border-border hover:border-foreground',
                )}
              >
                <span
                  aria-hidden="true"
                  className={cn('flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2', on ? 'border-foreground' : 'border-input')}
                >
                  {on && <span className="h-2.5 w-2.5 rounded-full bg-foreground" />}
                </span>
                <span className="min-w-0">
                  <span className="font-semibold text-foreground">{optionText[o.purpose]}</span>
                  <span className="text-muted-foreground"> · {formatVnd(o.amount)}</span>
                </span>
              </button>
            )
          })}
        </div>
      )}

      <div className="mt-5 flex flex-col items-center gap-3 sm:flex-row sm:items-center sm:gap-6">
        <div className="shrink-0 border border-rule bg-card p-1">
          <QRCodeSVG
            value={current.payload}
            size={240}
            level="M"
            marginSize={4}
            title={`Mã VietQR — ${optionText[current.purpose]}`}
            data-testid="vietqr"
          />
        </div>
        <p className="max-w-[40ch] font-serif text-[0.95rem] italic leading-relaxed text-foreground">
          Mã QR đã có sẵn số tiền và nội dung — bạn không cần nhập tay.
        </p>
      </div>

      <Button asChild variant="outline" className="mt-4 h-11 w-full">
        <a href={qrPngUrl(q.id)(current.purpose)} download>
          <Download aria-hidden="true" />
          Lưu ảnh QR
        </a>
      </Button>
      <p className="mt-2 text-xs leading-relaxed text-muted-foreground">
        Đang xem trên chính điện thoại này? Bấm “Lưu ảnh QR” → Mở app ngân hàng → Quét QR → chọn ảnh vừa lưu.
      </p>

      <dl className="mt-4 border-t border-rule">
        <CopyRow label="Ngân hàng" what="tên ngân hàng" value={bank.shortName} copyText={bank.shortName} />
        <CopyRow label="Số tài khoản" what="số tài khoản" value={bank.accountNo} copyText={bank.accountNo} />
        <CopyRow label="Chủ tài khoản" what="chủ tài khoản" value={bank.accountName || '—'} copyText={bank.accountName} />
        <CopyRow label="Số tiền" what="số tiền" value={<MoneyText amount={current.amount} />} copyText={String(current.amount)} testId="qr-amount" />
        <CopyRow label="Nội dung" what="nội dung chuyển khoản" value={current.memo} copyText={current.memo} testId="qr-memo" />
      </dl>
      <p className="mt-3 font-serif text-[0.8125rem] italic leading-relaxed text-muted-foreground">
        {memoExplanation(current.purpose, current.memo, q.snapshot.number)}
      </p>

      <div className="mt-5 space-y-3">
        {pending && (
          <p role="status" className="border-l-4 border-success py-1 pl-4 font-serif text-[0.95rem] italic leading-relaxed text-foreground">
            {noticeText(pending)}
          </p>
        )}
        {pending?.purpose !== current.purpose && (
          <Button type="button" variant="outline" className="h-11 w-full" onClick={() => setReporting(true)}>
            Tôi đã chuyển khoản
          </Button>
        )}
      </div>

      <Dialog open={reporting} onOpenChange={setReporting}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Báo đã chuyển {formatVnd(current.amount)}</DialogTitle>
            <DialogDescription>Nhà thiết kế sẽ kiểm tra tài khoản và xác nhận đã nhận tiền.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="notice-note">Ghi chú cho nhà thiết kế (tuỳ chọn)</Label>
            <Textarea
              id="notice-note"
              maxLength={500}
              rows={3}
              value={note}
              onChange={e => setNote(e.target.value)}
              placeholder="Ví dụ: chuyển từ tài khoản của chồng"
            />
          </div>
          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-11" onClick={() => setReporting(false)}>Huỷ</Button>
            <Button type="button" className="h-11" disabled={notice.isPending} onClick={report}>
              {notice.isPending ? 'Đang gửi…' : 'Báo đã chuyển'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </EditorialSection>
  )
}
