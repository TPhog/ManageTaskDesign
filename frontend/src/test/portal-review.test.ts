import { approveErrorMessage, deliverableBadge, progressText, reviewState } from '@/features/portal/review'

describe('reviewState', () => {
  it('follows the server state (BR-48)', () => {
    expect(reviewState({ reviewState: 'AWAITING_CLIENT' })).toBe('AWAITING')
    expect(reviewState({ reviewState: 'CLIENT_APPROVED' })).toBe('APPROVED_BY_CLIENT')
    expect(reviewState({ reviewState: 'CHANGES_IN_PROGRESS' })).toBe('CHANGES_IN_PROGRESS')
    expect(reviewState({ reviewState: 'NONE' })).toBe('NONE')
  })
})

describe('deliverableBadge (CF-01/02)', () => {
  it('speaks with one voice', () => {
    expect(deliverableBadge({ status: 'IN_REVIEW', reviewState: 'AWAITING_CLIENT' })).toEqual({ status: 'IN_REVIEW', label: 'Chờ bạn duyệt' })
    expect(deliverableBadge({ status: 'IN_REVIEW', reviewState: 'CLIENT_APPROVED' })).toEqual({ status: 'APPROVED', label: 'Bạn đã duyệt — chờ xác nhận' })
    expect(deliverableBadge({ status: 'IN_REVIEW', reviewState: 'CHANGES_IN_PROGRESS' })).toEqual({ status: 'IN_PROGRESS', label: 'Đang sửa theo yêu cầu của bạn' })
    expect(deliverableBadge({ status: 'DELIVERED', reviewState: 'NONE' })).toEqual({ status: 'DELIVERED', label: 'Đã bàn giao' })
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
  it('explains why the approval did not go through', () => {
    expect(approveErrorMessage({ status: 409, code: 'CHANGE_REQUEST_OPEN' }))
      .toBe('Bạn đang có yêu cầu sửa cho sản phẩm này — chờ nhà thiết kế gửi bản mới rồi duyệt.')
    expect(approveErrorMessage({ status: 409 })).toBe('Sản phẩm không còn chờ duyệt — trang đã được tải lại.')
    expect(approveErrorMessage({ status: 404 })).toBe('Không tìm thấy sản phẩm này.')
    expect(approveErrorMessage(new Error('network'))).toBe('Không gửi được. Vui lòng thử lại.')
  })
})
