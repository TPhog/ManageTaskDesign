import { formatVnd } from '@/lib/format'
import {
  PAYMENT_REMINDER_TEMPLATE,
  PORTAL_INVITE_TEMPLATE,
  QUOTATION_SENT_TEMPLATE,
  paymentReminderMessage,
  quotationNumberLabel,
  quotationSentMessage,
  renderTemplate,
} from '@/lib/messageTemplates'
import type { QuotationDetail } from '@/lib/quotationTypes'
import { portalInviteMessage } from '@/lib/portalLink'

describe('renderTemplate', () => {
  it('fills every placeholder, including ones with spaces and diacritics', () => {
    expect(renderTemplate('Chào {tên}, link: {link portal}', { 'tên': 'Lan', 'link portal': 'http://x/p/a' }))
      .toBe('Chào Lan, link: http://x/p/a')
  })

  it('replaces a placeholder used twice', () => {
    expect(renderTemplate('{tên} — {tên}', { 'tên': 'A' })).toBe('A — A')
  })

  it('leaves unknown and empty placeholders visible', () => {
    expect(renderTemplate('{tên} {số}', { 'tên': '', khác: 'x' })).toBe('{tên} {số}')
  })

  it('never expands placeholders that come from inserted values', () => {
    expect(renderTemplate('Chào {tên}', { 'tên': '{số}', 'số': 'BG' })).toBe('Chào {số}')
  })
})

describe('quotationSentMessage (docs/08 §2.3 bước 6, also COPY_QUOTATION_MESSAGE)', () => {
  const sent = { clientName: 'Nguyễn Văn A', number: 'BG-2026-0007', version: 1, total: 2_400_000, snapshot: null }

  it('keeps the docs placeholders in the template', () => {
    expect(QUOTATION_SENT_TEMPLATE).toBe(
      'Chào anh/chị {tên}, em gửi báo giá {số} ({tổng}). Anh/chị xem và quét QR tại: {link portal}',
    )
  })

  it('includes the portal link the designer just created', () => {
    expect(quotationSentMessage(sent, 'https://app/p/tok')).toBe(
      `Chào anh/chị Nguyễn Văn A, em gửi báo giá BG-2026-0007 (${formatVnd(2_400_000)}). Anh/chị xem và quét QR tại: https://app/p/tok`,
    )
  })

  it('reminds the client to open the old portal link when no new link exists', () => {
    expect(quotationSentMessage({ ...sent, version: 2, total: 1_300_000 }, null)).toBe(
      `Chào anh/chị Nguyễn Văn A, em gửi báo giá BG-2026-0007 v2 (${formatVnd(1_300_000)}). Anh/chị mở link portal em đã gửi trước đó để xem và quét QR.`,
    )
  })

  it('prefers the snapshot taken when the quotation was sent (BR-39)', () => {
    const snapshot = { client: { name: 'Tên lúc gửi', company: null }, number: 'BG-2026-0007', version: 1, total: 999 } as unknown as QuotationDetail['snapshot']
    const m = quotationSentMessage({ ...sent, clientName: 'Tên mới', total: 5, snapshot }, null)
    expect(m).toContain('Chào anh/chị Tên lúc gửi,')
    expect(m).toContain(`(${formatVnd(999)})`)
  })
})

describe('paymentReminderMessage (COPY_PAYMENT_REMINDER, moved from flowUtils)', () => {
  const q = {
    clientName: 'Chị Lan', projectName: 'Logo ABC', number: 'BG-2026-0007', version: 2,
    paymentStatus: 'UNPAID' as const, depositPercent: 50, depositAmount: 1_500_000, outstanding: 3_000_000,
  }

  it('asks for the deposit first, with the template wording', () => {
    expect(PAYMENT_REMINDER_TEMPLATE).toContain('{khoản}')
    expect(paymentReminderMessage(q)).toBe([
      'Chào Chị Lan,',
      `Mình nhắc nhẹ tiền cọc ${formatVnd(1_500_000)} của báo giá BG-2026-0007 v2 (dự án Logo ABC).`,
      'Bạn quét mã QR trong trang theo dõi dự án hoặc chuyển khoản theo thông tin trên báo giá giúp mình nhé. Cảm ơn bạn!',
    ].join('\n'))
  })

  it('asks for the rest once the deposit is paid or when there is no deposit', () => {
    expect(paymentReminderMessage({ ...q, paymentStatus: 'DEPOSIT_PAID', outstanding: 1_500_000 }))
      .toContain(`số tiền còn lại ${formatVnd(1_500_000)}`)
    expect(paymentReminderMessage({ ...q, depositPercent: 0, depositAmount: 0 }))
      .toContain(`số tiền còn lại ${formatVnd(3_000_000)}`)
  })

  it('names an unnumbered quotation "nháp" like the flow warnings', () => {
    expect(paymentReminderMessage({ ...q, number: null })).toContain('của báo giá nháp (dự án Logo ABC)')
  })
})

describe('quotationNumberLabel', () => {
  it('shows the version only from v2 on', () => {
    expect(quotationNumberLabel('BG-2026-0007', 1)).toBe('BG-2026-0007')
    expect(quotationNumberLabel('BG-2026-0007', 3)).toBe('BG-2026-0007 v3')
  })
})

describe('portal invite (Plan 02 message, now a template)', () => {
  it('renders the portal invite template', () => {
    expect(PORTAL_INVITE_TEMPLATE).toContain('{link portal}')
    expect(portalInviteMessage('Chị Lan', 'http://localhost:5173/p/abc')).toBe([
      'Chào Chị Lan,',
      'Mình gửi bạn link theo dõi tiến độ dự án: http://localhost:5173/p/abc',
      'Bạn mở link để xem trạng thái, các hạng mục và tiến trình công việc, không cần đăng nhập.',
      'Link dành riêng cho bạn, vui lòng không chuyển tiếp cho người khác nhé.',
    ].join('\n'))
  })
})
