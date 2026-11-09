import type { ApiError } from '@/lib/api'
import { formatDatetime, formatVnd } from '@/lib/format'
import type { PortalPaymentNotice, PortalQuotationDetail } from '@/lib/portalTypes'
import type { EffectiveStatus, PaymentStatus, QrPurpose, QuotationStatus } from '@/lib/quotationTypes'
import { portalQuotationStatus } from './labels'

/** "BG-2026-0007" for v1, "BG-2026-0007 · Phiên bản 2" afterwards. */
export function versionText(q: { number: string; version: number }): string {
  return q.version > 1 ? `${q.number} · Phiên bản ${q.version}` : q.number
}

export type NoticeTone = 'info' | 'warning' | 'success' | 'muted'

export interface QuotationNotice {
  tone: NoticeTone
  text: string
}

/** docs/08 §3.2: what the client should know about the quotation's state; null = nothing to add. */
export function quotationNotice(
  q: { effectiveStatus: EffectiveStatus; paymentStatus: PaymentStatus; clientAcceptedAt?: string | null },
  designerName: string,
): QuotationNotice | null {
  const who = designerName || 'nhà thiết kế'
  if (q.effectiveStatus === 'SENT' && q.clientAcceptedAt) {
    return { tone: 'success', text: 'Bạn đã đồng ý báo giá này — đang chờ nhà thiết kế xác nhận.' }
  }
  switch (q.effectiveStatus) {
    case 'EXPIRED':
      return { tone: 'warning', text: `Báo giá đã hết hạn. Vui lòng liên hệ ${who} để xác nhận lại.` }
    case 'REJECTED':
      return { tone: 'muted', text: 'Báo giá này đã được ghi nhận là không tiếp tục.' }
    case 'SUPERSEDED':
      return { tone: 'muted', text: 'Đây là phiên bản cũ — đã có phiên bản mới thay thế.' }
    case 'ACCEPTED':
      if (q.paymentStatus === 'PAID') return { tone: 'success', text: 'Đã thanh toán đầy đủ. Cảm ơn bạn!' }
      if (q.paymentStatus === 'DEPOSIT_PAID') {
        return { tone: 'info', text: 'Đã nhận tiền cọc. Bạn có thể thanh toán phần còn lại bằng mã QR bên dưới.' }
      }
      return { tone: 'info', text: 'Báo giá đã được xác nhận.' }
    default:
      return null
  }
}

/** Payment status only means something once the designer recorded the quotation as accepted. */
export function showsPaymentStatus(q: { status: QuotationStatus }): boolean {
  return q.status === 'ACCEPTED'
}

/** GET /api/portal/quotations/{id}/qr/{purpose}.png */
export function qrPngUrl(quotationId: string): (purpose: QrPurpose) => string {
  return purpose => `/api/portal/quotations/${quotationId}/qr/${purpose}.png`
}

/** CF-03: the badge once the client agreed — status stays SENT until the designer confirms. */
export function quotationBadgeLabel(q: { effectiveStatus: EffectiveStatus; status: QuotationStatus; clientAcceptedAt: string | null }): string {
  if (q.effectiveStatus === 'SENT' && q.clientAcceptedAt) return 'Bạn đã đồng ý — chờ xác nhận'
  return portalQuotationStatus[q.effectiveStatus] ?? q.effectiveStatus
}

/** CF-03: "Đồng ý báo giá" only on a live (SENT, not expired) quotation the client has not agreed to yet. */
export function canAccept(q: Pick<PortalQuotationDetail, 'status' | 'effectiveStatus' | 'clientAcceptedAt'>): boolean {
  return q.status === 'SENT' && q.effectiveStatus === 'SENT' && !q.clientAcceptedAt
}

export interface PaymentSummary {
  /** what the client should transfer now */
  now: number
  /** what is left after handover; 0 = nothing more */
  later: number
}

/** CF-05 (same BR-75 table as the backend): what to pay now and what is left after handover; null = nothing to pay. */
export function paymentSummary(
  q: Pick<PortalQuotationDetail, 'status' | 'effectiveStatus' | 'paymentStatus' | 'snapshot'>,
): PaymentSummary | null {
  const { total, depositAmount, depositPercent } = q.snapshot
  const awaiting = q.effectiveStatus === 'SENT' || q.effectiveStatus === 'EXPIRED'
    || (q.effectiveStatus === 'ACCEPTED' && q.paymentStatus === 'UNPAID')
  if (awaiting) return depositPercent > 0 ? { now: depositAmount, later: total - depositAmount } : { now: total, later: 0 }
  if (q.effectiveStatus === 'ACCEPTED' && q.paymentStatus === 'DEPOSIT_PAID') return { now: total - depositAmount, later: 0 }
  return null
}

const memoMeaning: Record<QrPurpose, string> = {
  deposit: 'tiền cọc (COC = cọc)',
  remaining: 'phần còn lại (CONLAI = còn lại)',
  full: 'tiền thanh toán toàn bộ (TT = thanh toán)',
}

/** CF-04: explains the unchanged VietQR memo (docs/06) in plain words. */
export function memoExplanation(purpose: QrPurpose, memo: string, number: string): string {
  return `Nội dung “${memo}” giúp nhà thiết kế biết đây là ${memoMeaning[purpose]} của báo giá ${number}. Giữ nguyên nội dung này khi chuyển.`
}

/** Plan 12 BR-77: what the client already reported. */
export function noticeText(n: PortalPaymentNotice): string {
  return `Bạn đã báo chuyển ${formatVnd(n.amount)} lúc ${formatDatetime(n.at)} — nhà thiết kế sẽ kiểm tra và xác nhận đã nhận tiền.`
}

/** Errors of POST …/accept and …/payment-notice. */
export function statementErrorMessage(err: unknown): string {
  const e = (err ?? {}) as Partial<ApiError>
  if (e.status === 409) {
    if (e.code === 'QUOTATION_EXPIRED') return 'Báo giá đã hết hạn — vui lòng liên hệ nhà thiết kế.'
    if (e.code === 'PAYMENT_NOT_APPLICABLE') return 'Khoản này hiện không cần chuyển — trang đã được tải lại.'
    if (e.code === 'INVALID_TRANSITION' || e.code === 'CONCURRENCY_CONFLICT') return 'Báo giá vừa được cập nhật — trang đã được tải lại.'
  }
  if (e.status === 404) return 'Không tìm thấy báo giá này.'
  return 'Không gửi được. Vui lòng thử lại.'
}
