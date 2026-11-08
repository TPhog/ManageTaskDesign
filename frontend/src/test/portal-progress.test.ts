import { deliveredText, progressOf, remainingRoundsText, waitingText } from '@/features/portal/progress'

describe('progressOf', () => {
  it('counts approved and delivered deliverables as done', () => {
    expect(progressOf({ total: 4, delivered: 1, approved: 1, inReview: 1 })).toEqual({ done: 2, pct: 50 })
  })

  it('is 0% when there are no deliverables', () => {
    expect(progressOf({ total: 0, delivered: 0, approved: 0, inReview: 0 }).pct).toBe(0)
  })

  it('rounds to the nearest percent', () => {
    expect(progressOf({ total: 3, delivered: 2, approved: 0, inReview: 0 }).pct).toBe(67)
  })
})

describe('deliveredText', () => {
  it('shows delivered over total', () => {
    expect(deliveredText({ total: 2, delivered: 1, approved: 0, inReview: 0 })).toBe('1/2 hạng mục đã bàn giao')
  })

  it('handles projects without deliverables', () => {
    expect(deliveredText({ total: 0, delivered: 0, approved: 0, inReview: 0 })).toBe('Chưa có hạng mục')
  })
})

describe('waitingText (QA C-06)', () => {
  it('says how many deliverables wait for the client', () => {
    expect(waitingText({ total: 3, delivered: 0, approved: 0, inReview: 1 })).toBe('1 hạng mục chờ bạn duyệt')
  })

  it('is empty when nothing waits', () => {
    expect(waitingText({ total: 3, delivered: 1, approved: 1, inReview: 0 })).toBe('')
  })
})

describe('remainingRoundsText', () => {
  it('shows remaining free revisions', () => {
    expect(remainingRoundsText({ includedRounds: 3, remainingRounds: 1 })).toBe('Còn 1/3 lần sửa miễn phí')
  })

  it('handles deliverables without free revisions', () => {
    expect(remainingRoundsText({ includedRounds: 0, remainingRounds: 0 })).toBe('Không gồm lần sửa miễn phí')
  })
})
