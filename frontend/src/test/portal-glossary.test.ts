import { portalBilling, portalDeliverableStatus, portalQuotationStatus, portalRequestStatus, portalWorkStatus } from '@/features/portal/labels'
import { roundTitle } from '@/features/portal/revisions'
import { studioInitials } from '@/features/portal/contact'

const JARGON = /hạng mục|lần sửa|brief|designer|round/i

describe('portal glossary (CF-10)', () => {
  it('uses plain words for every label', () => {
    for (const map of [portalBilling, portalDeliverableStatus, portalQuotationStatus, portalRequestStatus, portalWorkStatus])
      for (const label of Object.values(map)) expect(label).not.toMatch(JARGON)
    expect(portalRequestStatus.NEW).toBe('Đã gửi — chờ nhà thiết kế xem')
    expect(portalRequestStatus.TRIAGED).toBe('Nhà thiết kế đã nhận, đang xử lý')
    expect(portalBilling.IN_QUOTA).toBe('Dùng lượt miễn phí')
    expect(portalQuotationStatus.SENT).toBe('Chờ bạn đồng ý')
  })

  it('names a round "Lượt chỉnh sửa"', () => {
    expect(roundTitle({ roundNo: 1, receivedAt: '2026-11-08' })).toBe('Lượt chỉnh sửa 1 · 08/11/2026')
  })

  it('makes a two-letter logo from the studio name', () => {
    expect(studioInitials('Minh Anh Design Studio')).toBe('MA')
    expect(studioInitials('cam')).toBe('C')
    expect(studioInitials('  ')).toBe('?')
  })
})
