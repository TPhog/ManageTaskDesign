import {
  MAX_LINE_PRICE,
  additionalQuotationBody,
  linePriceError,
  pickService,
  pricedLinesFromPreview,
  zeroPricedLineNames,
} from '@/features/admin/revisions/revisionUtils'
import type { AdditionalQuotationPreview } from '@/lib/revisionTypes'

const preview: AdditionalQuotationPreview = {
  lines: [
    { kind: 'ROUND', revisionItemIds: ['m1', 'm2'], description: 'Vòng sửa thứ 4 — Logo', details: '• Đổi màu\n• Đổi font', suggestedUnitPrice: 200_000 },
    { kind: 'NEW_DELIVERABLE', revisionItemIds: ['n1'], description: 'Thiết kế sticker', details: '', suggestedUnitPrice: 0 },
    { kind: 'SCOPE_CHANGE', revisionItemIds: ['s1'], description: 'Bản tiếng Anh', details: '', suggestedUnitPrice: 0 },
  ],
}

describe('additional quotation pricing (plan 09)', () => {
  it('starts every line at its suggested price without a service', () => {
    expect(pricedLinesFromPreview(preview).map(p => [p.unitPrice, p.serviceId])).toEqual([[200_000, null], [0, null], [0, null]])
  })

  it('picking a service fills its base price and name; clearing keeps the typed price', () => {
    const [, sticker] = pricedLinesFromPreview(preview)
    const picked = pickService(sticker, { id: 'svc', name: 'Sticker mẫu', basePrice: 300_000 })
    expect([picked.serviceId, picked.serviceName, picked.unitPrice]).toEqual(['svc', 'Sticker mẫu', 300_000])
    const cleared = pickService({ ...picked, unitPrice: 320_000 }, null)
    expect([cleared.serviceId, cleared.serviceName, cleared.unitPrice]).toEqual([null, null, 320_000])
  })

  it('sends new deliverables in newDeliverableServices and other lines in linePrices, keyed by the first item', () => {
    const [round, sticker, scope] = pricedLinesFromPreview(preview)
    const body = additionalQuotationBody(['m1', 'm2', 'n1', 's1'], [
      { ...round, unitPrice: 350_000 },
      pickService(sticker, { id: 'svc', name: 'Sticker mẫu', basePrice: 300_000 }),
      { ...scope, unitPrice: 450_000 },
    ])
    expect(body).toEqual({
      revisionItemIds: ['m1', 'm2', 'n1', 's1'],
      newDeliverableServices: [{ revisionItemId: 'n1', serviceId: 'svc', unitPrice: 300_000 }],
      linePrices: [{ revisionItemId: 'm1', unitPrice: 350_000 }, { revisionItemId: 's1', unitPrice: 450_000 }],
    })
  })

  it('names the lines still at 0 ₫ (service name once picked)', () => {
    const [round, sticker, scope] = pricedLinesFromPreview(preview)
    expect(zeroPricedLineNames([round, sticker, scope])).toEqual(['Thiết kế sticker', 'Bản tiếng Anh'])
    expect(zeroPricedLineNames([{ ...pickService(sticker, { id: 'x', name: 'Sticker mẫu', basePrice: 0 }) }])).toEqual(['Sticker mẫu'])
  })

  it('accepts 0 … 100 tỷ ₫ whole numbers only', () => {
    expect(linePriceError(0)).toBeNull()
    expect(linePriceError(MAX_LINE_PRICE)).toBeNull()
    expect(linePriceError(MAX_LINE_PRICE + 1)).toBe('Đơn giá từ 0 đến 100.000.000.000 ₫')
    expect(linePriceError(-1)).toBe('Đơn giá từ 0 đến 100.000.000.000 ₫')
    expect(linePriceError(1.5)).toBe('Đơn giá từ 0 đến 100.000.000.000 ₫')
  })
})
