import { approveErrorMessage, progressText, reviewState } from '@/features/portal/review'

describe('reviewState', () => {
  it('asks for a decision only while the deliverable is under review', () => {
    expect(reviewState({ status: 'IN_REVIEW', clientDecision: null })).toBe('AWAITING')
    expect(reviewState({ status: 'IN_REVIEW', clientDecision: 'APPROVED' })).toBe('APPROVED_BY_CLIENT')
    for (const status of ['NOT_STARTED', 'IN_PROGRESS', 'APPROVED', 'DELIVERED', 'CANCELLED'] as const)
      expect(reviewState({ status, clientDecision: null })).toBe('NONE')
  })
})

describe('progressText', () => {
  it('shows the task percentage and nothing without tasks', () => {
    expect(progressText({ progressPercent: 50 })).toBe('Tiến độ 50%')
    expect(progressText({ progressPercent: 0 })).toBe('Tiến độ 0%')
    expect(progressText({ progressPercent: null })).toBeNull()
  })
})

describe('approveErrorMessage', () => {
  it('explains a deliverable that is no longer under review', () => {
    expect(approveErrorMessage({ status: 409 })).toBe('Hạng mục không còn chờ duyệt — trang đã được tải lại.')
    expect(approveErrorMessage({ status: 404 })).toBe('Không tìm thấy hạng mục này.')
    expect(approveErrorMessage(new Error('network'))).toBe('Không gửi được. Vui lòng thử lại.')
  })
})
