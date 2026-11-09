import { buildTodoList } from '@/features/portal/todo'
import { formatVnd } from '@/lib/format'
import type { PortalProjectListItem, PortalQuotationListItem } from '@/lib/portalTypes'

const project = (over: Partial<PortalProjectListItem> = {}): PortalProjectListItem => ({
  id: 'p1', name: 'Tiệm bánh Mây', designType: 'LOGO', status: 'IN_PROGRESS', dueDate: null,
  deliverableSummary: { total: 3, delivered: 0, approved: 0, inReview: 2, awaitingClient: 2 },
  hasOpenQuotation: true, openQuotationId: 'q1', ...over,
})
const quote = (over: Partial<PortalQuotationListItem> = {}): PortalQuotationListItem => ({
  id: 'q1', projectId: 'p1', projectName: 'Tiệm bánh Mây', number: 'BG-2026-0001', version: 1, kind: 'INITIAL',
  title: 'Báo giá', status: 'SENT', effectiveStatus: 'SENT', paymentStatus: 'UNPAID', total: 7_020_000,
  depositAmount: 3_510_000, issueDate: '2026-11-08', expiryDate: '2026-11-15', clientAcceptedAt: null,
  clientPaymentNotice: null, ...over,
})

describe('buildTodoList (CF-14)', () => {
  it('orders: agree to the quotation, then pay, then review', () => {
    expect(buildTodoList([project()], [quote()]).map(t => t.label)).toEqual([
      'Xem và đồng ý báo giá BG-2026-0001',
      'Duyệt 2 sản phẩm của Tiệm bánh Mây',
    ])
    const agreed = buildTodoList([project()], [quote({ clientAcceptedAt: '2026-11-08T03:00:00Z' })])
    expect(agreed.map(t => t.label)).toEqual([
      `Chuyển tiền cọc ${formatVnd(3_510_000)} (BG-2026-0001)`,
      'Duyệt 2 sản phẩm của Tiệm bánh Mây',
    ])
    expect(agreed[0]).toMatchObject({ kind: 'PAYMENT', to: '/portal/quotations/q1' })
  })

  it('drops a payment the client already reported and an expired quotation', () => {
    const reported = quote({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED',
      clientPaymentNotice: { purpose: 'deposit', amount: 3_510_000, at: '2026-11-08T03:00:00Z', note: null, pending: true } })
    expect(buildTodoList([project({ deliverableSummary: { total: 1, delivered: 0, approved: 0, inReview: 0, awaitingClient: 0 } })],
      [reported, quote({ id: 'q2', effectiveStatus: 'EXPIRED' })])).toEqual([])
  })

  it('asks for the remaining amount only once something was delivered', () => {
    const half = quote({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', paymentStatus: 'DEPOSIT_PAID' })
    const summary = { total: 1, delivered: 0, approved: 0, inReview: 0, awaitingClient: 0 }
    expect(buildTodoList([project({ deliverableSummary: summary })], [half])).toEqual([])
    expect(buildTodoList([project({ deliverableSummary: { ...summary, delivered: 1 } })], [half])[0].label)
      .toBe(`Chuyển phần còn lại ${formatVnd(3_510_000)} (BG-2026-0001)`)
  })
})
