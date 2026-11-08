import { confirmableWarnings, projectTransitions, statusErrorMessage } from '@/features/admin/projects/detail/projectStatus'

describe('projectTransitions (docs/02 §3)', () => {
  it('lets a completed project be reopened with a reason', () =>
    expect(projectTransitions.COMPLETED).toEqual([{ to: 'IN_PROGRESS', label: 'Mở lại dự án', reason: 'reopen' }]))

  it('treats CANCELLED as final', () => expect(projectTransitions.CANCELLED).toEqual([]))

  it('asks a reason for hold and cancel', () => {
    expect(projectTransitions.IN_PROGRESS.find(t => t.to === 'ON_HOLD')?.reason).toBe('hold')
    expect(projectTransitions.DRAFT.find(t => t.to === 'CANCELLED')?.reason).toBe('cancel')
    expect(projectTransitions.IN_PROGRESS.find(t => t.to === 'COMPLETED')?.reason).toBe('none')
  })
})

describe('confirmableWarnings', () => {
  it('returns the warnings of a 422 BUSINESS_RULE', () =>
    expect(confirmableWarnings({ status: 422, code: 'BUSINESS_RULE', warnings: ['Còn báo giá đã chấp nhận chưa thu đủ tiền.'] }))
      .toEqual(['Còn báo giá đã chấp nhận chưa thu đủ tiền.']))

  it('ignores other errors', () => {
    expect(confirmableWarnings({ status: 409, code: 'INVALID_TRANSITION' })).toBeNull()
    expect(confirmableWarnings({ status: 422, code: 'BUSINESS_RULE', warnings: [] })).toBeNull()
  })
})

describe('statusErrorMessage', () => {
  it('shows the field error of a missing reason', () =>
    expect(statusErrorMessage({ status: 400, errors: { reason: ['Bắt buộc lý do khi hủy dự án'] } })).toBe('Bắt buộc lý do khi hủy dự án'))
  it('explains an invalid transition', () =>
    expect(statusErrorMessage({ status: 409, code: 'INVALID_TRANSITION' })).toBe('Chuyển trạng thái không hợp lệ — tải lại trang để xem trạng thái mới nhất'))
})
