import { useState, type ReactNode } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Copy, Download } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { MoneyText } from '@/components/MoneyText'
import { cn } from '@/lib/utils'
import type { QrOption, QrPurpose, SnapshotBank } from '@/lib/quotationTypes'

interface QrCardProps {
  options: QrOption[]
  /** from the quotation snapshot (docs/06 §5), never from current settings */
  bank: SnapshotBank
  /** PNG download URL for a purpose; omit to hide the button */
  pngUrl?: (purpose: QrPurpose) => string
}

function Line({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium text-foreground">{children}</dd>
    </div>
  )
}

/** docs/08 §1 + docs/06 §6: VietQR (EC level M, ≥ 240px) plus the same data as text for clients who cannot scan. */
export function QrCard({ options, bank, pngUrl }: QrCardProps) {
  const [selected, setSelected] = useState<QrPurpose | null>(null)
  const current = options.find(o => o.purpose === selected) ?? options[0]
  if (!current) return null

  async function copy(text: string, what: string) {
    try {
      await navigator.clipboard.writeText(text)
      toast.success(`Đã sao chép ${what}`)
    } catch {
      toast.error('Không sao chép được — hãy chọn và sao chép thủ công')
    }
  }

  return (
    <section aria-label="Thanh toán qua VietQR" className="rounded-xl border bg-card p-5 shadow-card">
      <h2 className="mb-3 font-display text-title font-bold text-foreground">Chuyển khoản VietQR</h2>
      {options.length > 1 && (
        <div role="radiogroup" aria-label="Chọn khoản thanh toán" className="mb-4 flex flex-wrap gap-2">
          {options.map(o => (
            <button
              key={o.purpose}
              type="button"
              role="radio"
              aria-checked={o.purpose === current.purpose}
              onClick={() => setSelected(o.purpose)}
              className={cn(
                'rounded-full border px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                o.purpose === current.purpose
                  ? 'border-primary bg-primary text-primary-foreground'
                  : 'border-border-strong bg-card text-foreground hover:bg-surface-sunk',
              )}
            >
              {o.label}
            </button>
          ))}
        </div>
      )}
      <div className="flex justify-center rounded-lg border bg-card p-3">
        <QRCodeSVG
          value={current.payload}
          size={240}
          level="M"
          marginSize={4}
          title={`Mã VietQR — ${current.label}`}
          data-testid="vietqr"
        />
      </div>
      <dl className="mt-4 space-y-3 text-sm">
        <Line label="Ngân hàng">{bank.shortName}</Line>
        <Line label="Số tài khoản">
          <span className="inline-flex items-center gap-1 font-mono">
            {bank.accountNo}
            <Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label="Sao chép số tài khoản" onClick={() => copy(bank.accountNo, 'số tài khoản')}>
              <Copy aria-hidden="true" />
            </Button>
          </span>
        </Line>
        <Line label="Chủ tài khoản">{bank.accountName || '—'}</Line>
        <Line label="Số tiền">
          <span data-testid="qr-amount" className="font-display text-xl font-bold"><MoneyText amount={current.amount} /></span>
        </Line>
        <Line label="Nội dung chuyển khoản">
          <span className="inline-flex items-center gap-1">
            <span data-testid="qr-memo" className="font-mono">{current.memo}</span>
            <Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label="Sao chép nội dung chuyển khoản" onClick={() => copy(current.memo, 'nội dung chuyển khoản')}>
              <Copy aria-hidden="true" />
            </Button>
          </span>
        </Line>
      </dl>
      {pngUrl && (
        <Button asChild variant="outline" size="sm" className="mt-4 w-full">
          <a href={pngUrl(current.purpose)} download>
            <Download aria-hidden="true" />
            Tải ảnh QR
          </a>
        </Button>
      )}
    </section>
  )
}
