import type { EffectiveStatus, PaymentStatus, QrPurpose, QuotationStatus } from '@/lib/quotationTypes'

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
  q: { effectiveStatus: EffectiveStatus; paymentStatus: PaymentStatus },
  designerName: string,
): QuotationNotice | null {
  const who = designerName || 'designer'
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

/** docs/08 §3.2: shown under the QR — the client confirms outside the app (ADR-004). */
export function afterPaymentHint(designerName: string): string {
  return `Sau khi đồng ý hoặc chuyển khoản, vui lòng nhắn cho ${designerName || 'designer'} để xác nhận.`
}

/** Payment status only means something once the designer recorded the quotation as accepted. */
export function showsPaymentStatus(q: { status: QuotationStatus }): boolean {
  return q.status === 'ACCEPTED'
}

/** GET /api/portal/quotations/{id}/qr/{purpose}.png */
export function qrPngUrl(quotationId: string): (purpose: QrPurpose) => string {
  return purpose => `/api/portal/quotations/${quotationId}/qr/${purpose}.png`
}
