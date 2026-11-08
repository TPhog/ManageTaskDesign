import {
  computeTotals,
  groupChains,
  paymentActions,
  percentHalfUp,
  quotationErrorMessage,
  quoteTargetWarnings,
  todayVn,
  zeroPriceRows,
  zeroPriceLineNames,
  deliverablePricesBody,
  acceptOutcome,
} from '@/features/admin/quotations/quotationUtils'
import type { QuotationListItem } from '@/lib/quotationTypes'

const item = (over: Partial<QuotationListItem>): QuotationListItem => ({
  id: 'q', projectId: 'p', projectName: 'P', clientName: 'C', kind: 'INITIAL', number: null, version: 1,
  title: 'T', status: 'DRAFT', effectiveStatus: 'DRAFT', paymentStatus: 'UNPAID', total: 0, depositAmount: 0,
  outstanding: 0, issueDate: null, expiryDate: null, createdAt: '2026-11-08T00:00:00Z', ...over,
})

describe('percentHalfUp (BR-03 mirror)', () => {
  it.each([
    [1_234_567, 8, 98_765],
    [12_345, 10, 1_235],
    [1_001, 50, 501],
    [1, 50, 1],
    [999, 0, 0],
  ])('%i × %i%% = %i', (amount, percent, expected) => {
    expect(percentHalfUp(amount, percent)).toBe(expected)
  })
})

describe('computeTotals', () => {
  it('matches AC-B1', () => {
    expect(computeTotals([{ quantity: 1, unitPrice: 2_000_000 }, { quantity: 1, unitPrice: 500_000 }], 100_000, 0, 50))
      .toEqual({ subtotal: 2_500_000, taxable: 2_400_000, vatAmount: 0, total: 2_400_000, depositAmount: 1_200_000 })
  })

  it('matches AC-B2', () => {
    const t = computeTotals([{ quantity: 1, unitPrice: 1_234_567 }], 0, 8, 0)
    expect(t.vatAmount).toBe(98_765)
    expect(t.total).toBe(1_333_332)
  })
})

describe('groupChains', () => {
  it('groups versions by number, newest version first, drafts without number on their own', () => {
    const chains = groupChains([
      item({ id: 'v1', number: 'BG-2026-0001', version: 1, status: 'SUPERSEDED', createdAt: '2026-11-01T00:00:00Z' }),
      item({ id: 'v2', number: 'BG-2026-0001', version: 2, status: 'SENT', createdAt: '2026-11-03T00:00:00Z' }),
      item({ id: 'd', number: null, createdAt: '2026-11-02T00:00:00Z' }),
    ])
    expect(chains.map(c => c.latest.id)).toEqual(['v2', 'd'])
    expect(chains[0].versions.map(v => v.version)).toEqual([2, 1])
  })
})

describe('paymentActions', () => {
  it('offers nothing before acceptance', () => {
    expect(paymentActions({ status: 'SENT', paymentStatus: 'UNPAID', depositPercent: 50 })).toEqual([])
  })

  it('offers deposit then full when unpaid with a deposit', () => {
    expect(paymentActions({ status: 'ACCEPTED', paymentStatus: 'UNPAID', depositPercent: 50 }).map(a => a.to))
      .toEqual(['DEPOSIT_PAID', 'PAID'])
  })

  it('skips deposit when there is none (BR-71)', () => {
    expect(paymentActions({ status: 'ACCEPTED', paymentStatus: 'UNPAID', depositPercent: 0 }).map(a => a.to))
      .toEqual(['PAID'])
  })

  it('marks backward steps as undo', () => {
    expect(paymentActions({ status: 'ACCEPTED', paymentStatus: 'PAID', depositPercent: 50 }))
      .toEqual([
        { to: 'DEPOSIT_PAID', label: 'Hoàn tác về Đã nhận cọc', undo: true },
        { to: 'UNPAID', label: 'Hoàn tác về Chưa thanh toán', undo: true },
      ])
  })
})

describe('todayVn', () => {
  it('uses the Asia/Ho_Chi_Minh calendar date', () => {
    expect(todayVn(new Date('2026-11-07T18:30:00Z'))).toBe('2026-11-08')
  })
})

describe('quotationErrorMessage', () => {
  it('maps conflict and lock codes to Vietnamese', () => {
    expect(quotationErrorMessage({ code: 'CONCURRENCY_CONFLICT' }, 'x')).toContain('tải lại')
    expect(quotationErrorMessage({ code: 'QUOTATION_LOCKED' }, 'x')).toContain('version mới')
  })

  it('prefers the first field error, then detail, then fallback', () => {
    expect(quotationErrorMessage({ errors: { vatPercent: ['VAT từ 0 đến 20%'] } }, 'x')).toBe('VAT từ 0 đến 20%')
    expect(quotationErrorMessage({ detail: 'Chưa cấu hình tài khoản nhận tiền' }, 'x')).toBe('Chưa cấu hình tài khoản nhận tiền')
    expect(quotationErrorMessage({}, 'Không thể lưu')).toBe('Không thể lưu')
  })
})

describe('quoteTargetWarnings (QA D-02/D-03)', () => {
  it('lists deliverables without a service and those already on a live quotation', () => {
    const w = quoteTargetWarnings([
      { name: 'Logo', serviceId: 's1', quotedIn: [{ quotationId: 'q', number: 'BG-2026-0028', version: 1, status: 'SENT', kind: 'INITIAL' }] },
      { name: 'Menu A4', serviceId: null, quotedIn: [{ quotationId: 'd', number: null, version: 1, status: 'DRAFT', kind: 'INITIAL' }] },
      { name: 'Banner', serviceId: 's2' },
    ])
    expect(w.noPrice).toEqual(['Menu A4'])
    expect(w.alreadyQuoted.map(a => a.name)).toEqual(['Logo'])
    expect(w.alreadyQuoted[0].refs[0].number).toBe('BG-2026-0028')
  })
})

describe('zeroPriceRows (QA D-03/D-04)', () => {
  it('returns the indexes of 0 ₫ lines', () => expect(zeroPriceRows([{ unitPrice: 1 }, { unitPrice: 0 }, { unitPrice: 0 }])).toEqual([1, 2]))
})

describe('zeroPriceLineNames (plan 09, DF-03)', () => {
  it('names each 0 ₫ line, falling back to its position when the description is blank', () => {
    expect(zeroPriceLineNames([
      { description: 'Logo', unitPrice: 2_000_000 },
      { description: '  Menu A5 ', unitPrice: 0 },
      { description: '', unitPrice: 0 },
    ])).toEqual(['Menu A5', 'Dòng 3'])
  })
})

describe('deliverablePricesBody (plan 09)', () => {
  it('sends typed prices only for deliverables without a service and above 0', () => {
    const targets = [{ id: 'a', serviceId: null }, { id: 'b', serviceId: 'svc' }, { id: 'c', serviceId: null }]
    expect(deliverablePricesBody(targets, { a: 450_000, b: 999, c: 0 })).toEqual([{ deliverableId: 'a', unitPrice: 450_000 }])
  })
})

describe('acceptOutcome (plan 09)', () => {
  it('says what accepting does per project status', () => {
    expect(acceptOutcome('IN_PROGRESS').text).toContain('Đang làm')
    expect(acceptOutcome('ON_HOLD').text).toContain('Đang làm')
    expect(acceptOutcome('QUOTED').text).toContain('Chưa bắt đầu')
    expect(acceptOutcome('COMPLETED').text).toContain('mở lại dự án')
    expect(acceptOutcome('CANCELLED').creates).toBe(false)
    expect(acceptOutcome('DRAFT').creates).toBe(true)
  })
})
