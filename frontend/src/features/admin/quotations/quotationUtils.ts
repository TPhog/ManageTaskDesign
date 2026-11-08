import type { ApiError } from '@/lib/api'
import { liveQuoteRefs } from '@/lib/quoteRefs'
import type { Deliverable, DeliverableQuoteRef, DeliverableStatus } from '@/lib/types'
import type { PaymentStatus, QuotationKind, QuotationListItem, QuotationStatus } from '@/lib/quotationTypes'

/** BR-03 mirror for the editor preview only (the server is authoritative): percent of whole đồng, half-up. */
export function percentHalfUp(amount: number, percent: number): number {
  return Math.floor((amount * percent + 50) / 100)
}

export interface MoneyPreview {
  subtotal: number
  taxable: number
  vatAmount: number
  total: number
  depositAmount: number
}

/** docs/02 §8.3 in the same order as Quotation.Recalculate on the backend. */
export function computeTotals(
  items: { quantity: number; unitPrice: number }[],
  discountAmount: number,
  vatPercent: number,
  depositPercent: number,
): MoneyPreview {
  const subtotal = items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0)
  const taxable = Math.max(0, subtotal - discountAmount)
  const vatAmount = percentHalfUp(taxable, vatPercent)
  const total = taxable + vatAmount
  return { subtotal, taxable, vatAmount, total, depositAmount: percentHalfUp(total, depositPercent) }
}

export interface QuotationChain {
  key: string
  latest: QuotationListItem
  versions: QuotationListItem[]
}

/** Versions sharing a number form one chain (a v1 draft has no number yet → its own chain); newest chain first. */
export function groupChains(items: QuotationListItem[]): QuotationChain[] {
  const byKey = new Map<string, QuotationListItem[]>()
  for (const q of items) {
    const key = q.number ?? q.id
    byKey.set(key, [...(byKey.get(key) ?? []), q])
  }
  return [...byKey.entries()]
    .map(([key, versions]) => {
      const sorted = versions.slice().sort((a, b) => b.version - a.version)
      return { key, latest: sorted[0], versions: sorted }
    })
    .sort((a, b) => b.latest.createdAt.localeCompare(a.latest.createdAt))
}

/** D1: only these deliverables can get their own quotation. */
export const isFinishedDeliverable = (status: DeliverableStatus) => status === 'APPROVED' || status === 'DELIVERED'

export interface QuoteTargetWarnings {
  /** names of deliverables without a service → their line starts at 0 ₫ (QUO-01) */
  noPrice: string[]
  /** deliverables already on a sent/accepted quotation → quoting them again bills twice */
  alreadyQuoted: { name: string; refs: DeliverableQuoteRef[] }[]
}

/** QA D-02/D-03: what to warn about before creating a draft from these deliverables. */
export function quoteTargetWarnings(targets: Pick<Deliverable, 'name' | 'serviceId' | 'quotedIn'>[]): QuoteTargetWarnings {
  return {
    noPrice: targets.filter(d => !d.serviceId).map(d => d.name),
    alreadyQuoted: targets
      .map(d => ({ name: d.name, refs: liveQuoteRefs(d.quotedIn) }))
      .filter(x => x.refs.length > 0),
  }
}

/** QA D-03/D-04: indexes of the lines whose unit price is 0 ₫. */
export const zeroPriceRows = (rows: { unitPrice: number }[]): number[] =>
  rows.flatMap((r, i) => (r.unitPrice === 0 ? [i] : []))

export interface PaymentAction {
  to: PaymentStatus
  label: string
  /** backward step — the API requires a reason */
  undo: boolean
}

/** docs/02 §8.4 steps offered on an ACCEPTED quotation. */
export function paymentActions(q: { status: QuotationStatus; paymentStatus: PaymentStatus; depositPercent: number }): PaymentAction[] {
  if (q.status !== 'ACCEPTED') return []
  const hasDeposit = q.depositPercent > 0
  switch (q.paymentStatus) {
    case 'UNPAID':
      return [
        ...(hasDeposit ? [{ to: 'DEPOSIT_PAID' as const, label: 'Đã nhận cọc', undo: false }] : []),
        { to: 'PAID', label: 'Đã nhận đủ', undo: false },
      ]
    case 'DEPOSIT_PAID':
      return [
        { to: 'PAID', label: 'Đã nhận đủ', undo: false },
        { to: 'UNPAID', label: 'Hoàn tác về Chưa thanh toán', undo: true },
      ]
    case 'PAID':
      return [
        ...(hasDeposit ? [{ to: 'DEPOSIT_PAID' as const, label: 'Hoàn tác về Đã nhận cọc', undo: true }] : []),
        { to: 'UNPAID', label: 'Hoàn tác về Chưa thanh toán', undo: true },
      ]
  }
}

export const kindLabel: Record<QuotationKind, string> = {
  INITIAL: 'Báo giá gốc',
  ADDITIONAL: 'Phát sinh',
}

/** Calendar date in Asia/Ho_Chi_Minh as 'YYYY-MM-DD' (BR-06) — default for accepted/paid date inputs. */
export function todayVn(now: Date = new Date()): string {
  return now.toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' })
}

/** Vietnamese message for a failed quotation request (docs/08 §4). */
export function quotationErrorMessage(err: Pick<ApiError, 'code' | 'detail' | 'errors'>, fallback: string): string {
  if (err.code === 'CONCURRENCY_CONFLICT') return 'Dữ liệu đã thay đổi ở tab khác — vui lòng tải lại trang'
  if (err.code === 'QUOTATION_LOCKED') return 'Báo giá đã gửi bị khóa — hãy tạo version mới để sửa'
  const firstFieldError = err.errors ? Object.values(err.errors)[0]?.[0] : undefined
  return firstFieldError ?? err.detail ?? fallback
}
