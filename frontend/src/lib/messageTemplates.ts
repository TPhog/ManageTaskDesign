import { formatVnd } from './format'
import type { QuotationDetail } from './quotationTypes'

/**
 * The only place that builds messages the designer pastes to a client (Zalo/email).
 * Fills `{placeholder}` with values (placeholders are written exactly as in docs/08 §2.3, e.g. `{link portal}`).
 * Unknown or empty placeholders stay visible so a gap is noticed before sending; inserted values are never scanned again.
 */
export function renderTemplate(template: string, values: Record<string, string | null | undefined>): string {
  return template.replace(/\{([^{}]+)\}/g, (match, key: string) => {
    const value = values[key]
    return value == null || value === '' ? match : value
  })
}

/** docs/08 §2.3 bước 6 — after sending a quotation, with the portal link the designer just created. */
export const QUOTATION_SENT_TEMPLATE =
  'Chào anh/chị {tên}, em gửi báo giá {số} ({tổng}). Anh/chị xem và quét QR tại: {link portal}'

/** Same message without a new link: the raw link is shown only once (07-security §4.1), so the client uses the old one. */
export const QUOTATION_SENT_NO_LINK_TEMPLATE =
  'Chào anh/chị {tên}, em gửi báo giá {số} ({tổng}). Anh/chị mở link portal em đã gửi trước đó để xem và quét QR.'

/** Plan 06b F4 COPY_PAYMENT_REMINDER (wording moved here from features/admin/flow/flowUtils.ts). */
export const PAYMENT_REMINDER_TEMPLATE = [
  'Chào {tên},',
  'Mình nhắc nhẹ {khoản} của báo giá {số} (dự án {dự án}).',
  'Bạn quét mã QR trong trang theo dõi dự án hoặc chuyển khoản theo thông tin trên báo giá giúp mình nhé. Cảm ơn bạn!',
].join('\n')

/** Portal invite sent with a new magic link (Plan 02 wording). */
export const PORTAL_INVITE_TEMPLATE = [
  'Chào {tên},',
  'Mình gửi bạn link theo dõi tiến độ dự án: {link portal}',
  'Bạn mở link để xem trạng thái, các hạng mục và tiến trình công việc, không cần đăng nhập.',
  'Link dành riêng cho bạn, vui lòng không chuyển tiếp cho người khác nhé.',
].join('\n')

/** `{số}`: the quotation number, with the version from v2 on. */
export function quotationNumberLabel(number: string, version: number): string {
  return version > 1 ? `${number} v${version}` : number
}

/**
 * docs/08 §2.3: the message after "Gửi" (portalUrl = link created in that dialog) and the flow's
 * COPY_QUOTATION_MESSAGE (portalUrl = null). Values come from the snapshot when there is one (BR-39).
 */
export function quotationSentMessage(
  q: Pick<QuotationDetail, 'clientName' | 'number' | 'version' | 'total' | 'snapshot'>,
  portalUrl: string | null,
): string {
  const s = q.snapshot
  return renderTemplate(portalUrl ? QUOTATION_SENT_TEMPLATE : QUOTATION_SENT_NO_LINK_TEMPLATE, {
    'tên': s?.client.name ?? q.clientName,
    'số': (s?.number ?? q.number) ? quotationNumberLabel((s?.number ?? q.number)!, s?.version ?? q.version) : 'nháp',
    'tổng': formatVnd(s?.total ?? q.total),
    'link portal': portalUrl,
  })
}

/** COPY_PAYMENT_REMINDER (F4): the deposit while nothing is paid, otherwise what is still due (BR-73). */
export function paymentReminderMessage(
  q: Pick<QuotationDetail,
    'clientName' | 'projectName' | 'number' | 'version' | 'paymentStatus' | 'depositPercent' | 'depositAmount' | 'outstanding'>,
): string {
  const deposit = q.paymentStatus === 'UNPAID' && q.depositPercent > 0
  return renderTemplate(PAYMENT_REMINDER_TEMPLATE, {
    'tên': q.clientName,
    'khoản': deposit ? `tiền cọc ${formatVnd(q.depositAmount)}` : `số tiền còn lại ${formatVnd(q.outstanding)}`,
    'số': q.number ? quotationNumberLabel(q.number, q.version) : 'nháp',
    'dự án': q.projectName,
  })
}
