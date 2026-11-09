import {
  canAccept, memoExplanation, paymentSummary, qrPngUrl, quotationBadgeLabel, quotationNotice,
  showsPaymentStatus, statementErrorMessage, versionText,
} from '@/features/portal/quotation'
import type { PortalQuotationDetail } from '@/lib/portalTypes'

describe('versionText', () => {
  it('shows only the number for the first version', () => {
    expect(versionText({ number: 'BG-2026-0007', version: 1 })).toBe('BG-2026-0007')
  })

  it('adds the version once the quotation was revised', () => {
    expect(versionText({ number: 'BG-2026-0007', version: 2 })).toBe('BG-2026-0007 · Phiên bản 2')
  })
})

describe('quotationNotice', () => {
  it('asks to reconfirm an expired quotation (docs/08 §3.2)', () => {
    expect(quotationNotice({ effectiveStatus: 'EXPIRED', paymentStatus: 'UNPAID' }, 'Lan')).toEqual({
      tone: 'warning',
      text: 'Báo giá đã hết hạn. Vui lòng liên hệ Lan để xác nhận lại.',
    })
  })

  it('falls back to "nhà thiết kế" without a display name', () => {
    expect(quotationNotice({ effectiveStatus: 'EXPIRED', paymentStatus: 'UNPAID' }, '')?.text).toContain('liên hệ nhà thiết kế')
  })

  it('says the client agreed and waits for the designer (CF-03)', () => {
    expect(quotationNotice({ effectiveStatus: 'SENT', paymentStatus: 'UNPAID', clientAcceptedAt: '2026-11-08T03:00:00Z' }, 'Lan'))
      .toEqual({ tone: 'success', text: 'Bạn đã đồng ý báo giá này — đang chờ nhà thiết kế xác nhận.' })
  })

  it('follows the payment status of an accepted quotation', () => {
    expect(quotationNotice({ effectiveStatus: 'ACCEPTED', paymentStatus: 'PAID' }, 'Lan'))
      .toEqual({ tone: 'success', text: 'Đã thanh toán đầy đủ. Cảm ơn bạn!' })
    expect(quotationNotice({ effectiveStatus: 'ACCEPTED', paymentStatus: 'DEPOSIT_PAID' }, 'Lan')?.tone).toBe('info')
    expect(quotationNotice({ effectiveStatus: 'ACCEPTED', paymentStatus: 'UNPAID' }, 'Lan')?.text).toBe('Báo giá đã được xác nhận.')
  })

  it('labels superseded and rejected versions', () => {
    expect(quotationNotice({ effectiveStatus: 'SUPERSEDED', paymentStatus: 'UNPAID' }, 'Lan')?.tone).toBe('muted')
    expect(quotationNotice({ effectiveStatus: 'REJECTED', paymentStatus: 'UNPAID' }, 'Lan')?.tone).toBe('muted')
  })

  it('adds nothing to an open quotation', () => {
    expect(quotationNotice({ effectiveStatus: 'SENT', paymentStatus: 'UNPAID' }, 'Lan')).toBeNull()
  })
})

describe('showsPaymentStatus', () => {
  it('only matters once the designer recorded the quotation as accepted', () => {
    expect(showsPaymentStatus({ status: 'ACCEPTED' })).toBe(true)
    expect(showsPaymentStatus({ status: 'SENT' })).toBe(false)
  })
})

describe('qrPngUrl', () => {
  it('points at the portal PNG endpoint', () => {
    expect(qrPngUrl('q1')('deposit')).toBe('/api/portal/quotations/q1/qr/deposit.png')
  })
})

describe('paymentSummary (CF-05)', () => {
  const snap = { total: 7_020_000, depositAmount: 3_510_000, depositPercent: 50 } as PortalQuotationDetail['snapshot']
  const remainingAmount = 3_510_000
  it('splits now and after handover', () => {
    expect(paymentSummary({ status: 'SENT', effectiveStatus: 'SENT', paymentStatus: 'UNPAID', remainingAmount, snapshot: snap })).toEqual({ now: 3_510_000, later: 3_510_000 })
    expect(paymentSummary({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', paymentStatus: 'DEPOSIT_PAID', remainingAmount, snapshot: snap })).toEqual({ now: 3_510_000, later: 0 })
    expect(paymentSummary({ status: 'SENT', effectiveStatus: 'SENT', paymentStatus: 'UNPAID', remainingAmount: 7_020_000, snapshot: { ...snap, depositPercent: 0, depositAmount: 0 } })).toEqual({ now: 7_020_000, later: 0 })
    expect(paymentSummary({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', paymentStatus: 'PAID', remainingAmount, snapshot: snap })).toBeNull()
  })

  it('takes the remaining amount from the backend, never total − deposit (R2 M5)', () => {
    expect(paymentSummary({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', paymentStatus: 'DEPOSIT_PAID', remainingAmount: 1, snapshot: snap })).toEqual({ now: 1, later: 0 })
  })
})

describe('memoExplanation (CF-04)', () => {
  it('explains the memo in plain words without changing it', () => {
    expect(memoExplanation('deposit', 'BG20260001 COC', 'BG-2026-0001'))
      .toBe('Nội dung “BG20260001 COC” giúp nhà thiết kế biết đây là tiền cọc (COC = cọc) của báo giá BG-2026-0001. Giữ nguyên nội dung này khi chuyển.')
  })
})

describe('canAccept / quotationBadgeLabel (CF-03)', () => {
  it('offers "Đồng ý" only on a live, not yet agreed quotation', () => {
    expect(canAccept({ status: 'SENT', effectiveStatus: 'SENT', clientAcceptedAt: null })).toBe(true)
    expect(canAccept({ status: 'SENT', effectiveStatus: 'EXPIRED', clientAcceptedAt: null })).toBe(false)
    expect(canAccept({ status: 'SENT', effectiveStatus: 'SENT', clientAcceptedAt: '2026-11-08T03:00:00Z' })).toBe(false)
    expect(quotationBadgeLabel({ status: 'SENT', effectiveStatus: 'SENT', clientAcceptedAt: '2026-11-08T03:00:00Z' })).toBe('Bạn đã đồng ý — chờ xác nhận')
    expect(quotationBadgeLabel({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', clientAcceptedAt: null })).toBe('Đã chốt')
  })
})

describe('statementErrorMessage', () => {
  it('maps the accept / payment-notice errors to plain words', () => {
    expect(statementErrorMessage({ status: 409, code: 'QUOTATION_EXPIRED' })).toBe('Báo giá đã hết hạn — vui lòng liên hệ nhà thiết kế.')
    expect(statementErrorMessage({ status: 409, code: 'PAYMENT_NOT_APPLICABLE' })).toBe('Khoản này hiện không cần chuyển — trang đã được tải lại.')
    expect(statementErrorMessage({ status: 409, code: 'CONCURRENCY_CONFLICT' })).toBe('Báo giá vừa được cập nhật — trang đã được tải lại.')
    expect(statementErrorMessage({ status: 404 })).toBe('Không tìm thấy báo giá này.')
    expect(statementErrorMessage(new Error('x'))).toBe('Không gửi được. Vui lòng thử lại.')
  })
})
