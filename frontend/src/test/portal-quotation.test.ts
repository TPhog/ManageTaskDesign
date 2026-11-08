import { afterPaymentHint, qrPngUrl, quotationNotice, showsPaymentStatus, versionText } from '@/features/portal/quotation'

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

  it('falls back to "designer" without a display name', () => {
    expect(quotationNotice({ effectiveStatus: 'EXPIRED', paymentStatus: 'UNPAID' }, '')?.text).toContain('liên hệ designer')
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

describe('afterPaymentHint', () => {
  it('names the designer to message after paying', () => {
    expect(afterPaymentHint('Lan')).toBe('Sau khi đồng ý hoặc chuyển khoản, vui lòng nhắn cho Lan để xác nhận.')
    expect(afterPaymentHint('')).toContain('nhắn cho designer')
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
