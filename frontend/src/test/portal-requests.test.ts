import type { ProjectStatus } from '@/lib/types'
import { acceptsRequests, requestErrorMessage, requestSchema, toRequestForm } from '@/features/portal/requests'

describe('acceptsRequests', () => {
  it('accepts open projects only (BR-21, BR-53)', () => {
    const open: ProjectStatus[] = ['DRAFT', 'QUOTED', 'IN_PROGRESS', 'ON_HOLD']
    expect(open.every(acceptsRequests)).toBe(true)
    expect(acceptsRequests('COMPLETED')).toBe(false)
    expect(acceptsRequests('CANCELLED')).toBe(false)
  })
})

describe('requestSchema', () => {
  it('requires a project and non-blank content', () => {
    const result = requestSchema.safeParse({ projectId: '', deliverableId: '', content: '   ' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.map(i => i.path[0])).toEqual(expect.arrayContaining(['projectId', 'content']))
    }
  })

  it('trims content and caps it at 5000 characters', () => {
    expect(requestSchema.parse({ projectId: 'p1', deliverableId: '', content: '  Đổi màu  ' }).content).toBe('Đổi màu')
    expect(requestSchema.safeParse({ projectId: 'p1', deliverableId: '', content: 'a'.repeat(5001) }).success).toBe(false)
  })
})

describe('toRequestForm', () => {
  it('sends trimmed content and the chosen deliverable', () => {
    const form = toRequestForm({ deliverableId: 'd1', content: ' Sửa logo ' })
    expect(form.get('content')).toBe('Sửa logo')
    expect(form.get('deliverableId')).toBe('d1')
  })

  it('omits an empty deliverable', () => {
    expect(toRequestForm({ deliverableId: '', content: 'Sửa' }).has('deliverableId')).toBe(false)
  })
})

describe('requestErrorMessage', () => {
  it('explains the daily limit (BR-54)', () => {
    expect(requestErrorMessage({ status: 429 })).toContain('20 yêu cầu')
  })

  it('shows the business-rule detail of a closed project', () => {
    expect(requestErrorMessage({ status: 422, detail: 'Dự án đã hoàn thành' })).toBe('Dự án đã hoàn thành')
    expect(requestErrorMessage({ status: 422 })).toBe('Dự án này không nhận yêu cầu mới.')
  })

  it('surfaces the first field error and falls back to a generic message', () => {
    expect(requestErrorMessage({ status: 400, errors: { content: ['Nội dung dài 1–5000 ký tự'] } })).toBe('Nội dung dài 1–5000 ký tự')
    expect(requestErrorMessage({ status: 404 })).toBe('Không tìm thấy dự án này.')
    expect(requestErrorMessage(new Error('network'))).toBe('Không gửi được yêu cầu. Vui lòng thử lại.')
  })
})

describe('requestErrorMessage', () => {
  it('maps session and CSRF failures', () => {
    expect(requestErrorMessage({ status: 401 })).toBe('Phiên xem đã hết hạn — vui lòng mở lại link nhà thiết kế đã gửi.')
    expect(requestErrorMessage({ status: 403 })).toBe('Vui lòng tải lại trang rồi gửi lại.')
  })

  it('uses fixed messages for known 422 cases and never shows rule codes', () => {
    const closed = requestErrorMessage({ status: 422, detail: 'Dự án đã hoàn thành hoặc đã huỷ — không gửi yêu cầu mới được (BR-21)' })
    expect(closed).toContain('hoàn thành')
    expect(closed).not.toMatch(/BR-/)
    const limit = requestErrorMessage({ status: 422, detail: 'Mỗi ngày bạn gửi được tối đa 20 yêu cầu (BR-54)' })
    expect(limit).toContain('20 yêu cầu')
    expect(limit).not.toMatch(/BR-/)
  })

  it('strips rule codes from unknown 422 details and falls back when empty', () => {
    expect(requestErrorMessage({ status: 422, detail: 'Nội dung không hợp lệ (BR-99)' })).toBe('Nội dung không hợp lệ')
    expect(requestErrorMessage({ status: 422 })).toBe('Dự án này không nhận yêu cầu mới.')
  })

  it('keeps 429 and 404 messages', () => {
    expect(requestErrorMessage({ status: 429 })).toContain('20 yêu cầu')
    expect(requestErrorMessage({ status: 404 })).toBe('Không tìm thấy dự án này.')
  })
})
