import type { ApiError } from '@/lib/api'
import { formatDatetime, formatVnd } from '@/lib/format'
import { liveQuoteRefs } from '@/lib/quoteRefs'
import type { Deliverable, DeliverableQuoteRef, DeliverableStatus, ProjectStatus } from '@/lib/types'
import type { PaymentStatus, QrPurpose, QuotationDetail, QuotationKind, QuotationListItem, QuotationStatus } from '@/lib/quotationTypes'

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
  /** deliverables already on a sent/accepted quotation → quoting them again bills twice */
  alreadyQuoted: { name: string; refs: DeliverableQuoteRef[] }[]
}

/** QA D-02/D-03: what to warn about before creating a draft from these deliverables. */
export function quoteTargetWarnings(targets: Pick<Deliverable, 'name' | 'serviceId' | 'quotedIn'>[]): QuoteTargetWarnings {
  return {
    alreadyQuoted: targets
      .map(d => ({ name: d.name, refs: liveQuoteRefs(d.quotedIn) }))
      .filter(x => x.refs.length > 0),
  }
}

/** QA D-03/D-04: indexes of the lines whose unit price is 0 ₫. */
export const zeroPriceRows = (rows: { unitPrice: number }[]): number[] =>
  rows.flatMap((r, i) => (r.unitPrice === 0 ? [i] : []))

/** Plan 09 / DF-03: the send confirmation names every 0 ₫ line. */
export const zeroPriceLineNames = (rows: { description: string; unitPrice: number }[]): string[] =>
  rows.flatMap((r, i) => (r.unitPrice === 0 ? [r.description.trim() || `Dòng ${i + 1}`] : []))

/** DF-24: why "Gửi báo giá" / "Lưu nháp" is disabled — one line per row without a description (1-based). */
export const quotationSendBlockers = (rows: { description: string }[]): string[] =>
  rows.flatMap((r, i) => (r.description.trim() === '' ? [`Dòng ${i + 1} chưa có mô tả`] : []))

/** Plan 09: typed prices for deliverables without a service (blank / 0 = leave the line at 0 ₫). */
export const deliverablePricesBody = (
  targets: Pick<Deliverable, 'id' | 'serviceId'>[], prices: Record<string, number>,
): { deliverableId: string; unitPrice: number }[] =>
  targets.filter(d => !d.serviceId && (prices[d.id] ?? 0) > 0).map(d => ({ deliverableId: d.id, unitPrice: prices[d.id] }))

/** What accepting does with NEW_DELIVERABLE lines, by project status (server: AcceptedNewDeliverables). */
export function acceptOutcome(status: ProjectStatus): { creates: boolean; text: string } {
  if (status === 'CANCELLED') return { creates: false, text: 'Dự án đã huỷ nên sẽ không tạo hạng mục nào.' }
  if (status === 'IN_PROGRESS' || status === 'ON_HOLD')
    return { creates: true, text: 'Dự án đang chạy nên hạng mục mới ở trạng thái Đang làm, kèm công việc từ các bước mẫu của dịch vụ.' }
  if (status === 'COMPLETED')
    return { creates: true, text: 'Dự án đã hoàn thành: hạng mục mới ở trạng thái Chưa bắt đầu, chưa có công việc — mở lại dự án để bắt đầu.' }
  return { creates: true, text: 'Hạng mục mới ở trạng thái Chưa bắt đầu, chưa có công việc; bước mẫu được đề xuất khi bắt đầu dự án.' }
}

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

const statementPurposeText: Record<QrPurpose, string> = { deposit: 'tiền cọc', full: 'toàn bộ', remaining: 'phần còn lại' }

/** Plan 12 (BR-76/77): what the client stated on the portal and the designer still has to confirm. */
export function clientStatementNotes(q: Pick<QuotationDetail, 'status' | 'clientAcceptedAt' | 'clientPaymentNotice'>): string[] {
  const notes: string[] = []
  if (q.status === 'SENT' && q.clientAcceptedAt) {
    notes.push(`Khách đã bấm “Đồng ý báo giá” trên portal lúc ${formatDatetime(q.clientAcceptedAt)} — bấm “Đánh dấu đã chấp nhận” để xác nhận.`)
  }
  const n = q.clientPaymentNotice
  if (n?.pending) {
    notes.push(
      `Khách báo đã chuyển ${formatVnd(n.amount)} (${statementPurposeText[n.purpose]}) lúc ${formatDatetime(n.at)}`
      + `${n.note ? ` — “${n.note}”` : ''}. Kiểm tra tài khoản rồi ${q.status === 'SENT' ? 'đánh dấu đã chấp nhận và ' : ''}cập nhật trạng thái thanh toán.`,
    )
  }
  return notes
}
