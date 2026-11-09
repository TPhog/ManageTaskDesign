import { deliveredText, progressOf, remainingRoundsText, waitingText } from '@/features/portal/progress'

describe('progressOf', () => {
  it('counts approved and delivered deliverables as done', () => {
    expect(progressOf({ total: 4, delivered: 1, approved: 1, inReview: 1, awaitingClient: 1 })).toEqual({ done: 2, pct: 50 })
  })

  it('is 0% when there are no deliverables', () => {
    expect(progressOf({ total: 0, delivered: 0, approved: 0, inReview: 0, awaitingClient: 0 }).pct).toBe(0)
  })

  it('rounds to the nearest percent', () => {
    expect(progressOf({ total: 3, delivered: 2, approved: 0, inReview: 0, awaitingClient: 0 }).pct).toBe(67)
  })
})

describe('deliveredText (CF-09)', () => {
  it('counts approved and delivered products as done', () => {
    expect(deliveredText({ total: 3, delivered: 0, approved: 1, inReview: 1, awaitingClient: 1 })).toBe('Đã xong 1/3 sản phẩm')
  })

  it('handles projects without products', () => {
    expect(deliveredText({ total: 0, delivered: 0, approved: 0, inReview: 0, awaitingClient: 0 })).toBe('Chưa có sản phẩm')
  })
})

describe('waitingText (QA C-06, BR-48)', () => {
  it('counts only the products still waiting for the client', () => {
    expect(waitingText({ total: 3, delivered: 0, approved: 0, inReview: 2, awaitingClient: 1 })).toBe('1 sản phẩm chờ bạn duyệt')
  })

  it('is empty when nothing waits', () => {
    expect(waitingText({ total: 3, delivered: 1, approved: 1, inReview: 1, awaitingClient: 0 })).toBe('')
  })
})

describe('remainingRoundsText', () => {
  it('shows remaining free revisions', () => {
    expect(remainingRoundsText({ includedRounds: 3, remainingRounds: 1 })).toBe('Còn 1 lượt chỉnh sửa miễn phí')
  })

  it('says when the free revisions are used up', () => {
    expect(remainingRoundsText({ includedRounds: 2, remainingRounds: 0 })).toBe('Đã dùng hết lượt chỉnh sửa miễn phí')
  })

  it('handles products without free revisions', () => {
    expect(remainingRoundsText({ includedRounds: 0, remainingRounds: 0 })).toBe('Không gồm lượt chỉnh sửa miễn phí')
  })
})
