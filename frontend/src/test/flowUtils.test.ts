import {
  actionHref,
  flowErrorText,
  paymentReminderMessage,
  quotationMessage,
  stageLabel,
  stepperSummary,
} from '@/features/admin/flow/flowUtils'
import { formatDate, formatVnd } from '@/lib/format'

const none = { deliverableId: null, quotationId: null, requestId: null }

describe('stepperSummary', () => {
  it('numbers the current stage out of five', () => {
    expect(stepperSummary({ stage: 'EXECUTE' })).toBe('Giai đoạn 3/5 · Thực hiện')
    expect(stepperSummary({ stage: 'CLOSE' })).toBe('Giai đoạn 5/5 · Hoàn tất')
  })

  it('says when the project is cancelled', () => {
    expect(stepperSummary({ stage: 'CANCELLED' })).toBe('Dự án đã huỷ')
  })
})

describe('stageLabel', () => {
  it('uses the Vietnamese names of the spec', () => {
    expect([stageLabel.PREPARE, stageLabel.QUOTE, stageLabel.REVIEW]).toEqual(['Chuẩn bị', 'Báo giá', 'Duyệt & bàn giao'])
  })
})

describe('actionHref', () => {
  it('opens the matching tab of the project', () => {
    expect(actionHref('ADD_DELIVERABLE', 'p1', none)).toBe('/admin/projects/p1?tab=deliverables')
    expect(actionHref('CONFIRM_BRIEF', 'p1', none)).toBe('/admin/projects/p1?tab=brief')
    expect(actionHref('CREATE_QUOTATION', 'p1', none)).toBe('/admin/projects/p1?tab=quotations')
    expect(actionHref('CREATE_ADDITIONAL_QUOTATION', 'p1', none)).toBe('/admin/projects/p1?tab=revisions')
  })

  it('opens the target quotation or request', () => {
    expect(actionHref('SEND_QUOTATION', 'p1', { ...none, quotationId: 'q1' })).toBe('/admin/quotations/q1')
    expect(actionHref('RECORD_ACCEPTANCE', 'p1', { ...none, quotationId: 'q1' })).toBe('/admin/quotations/q1')
    expect(actionHref('TRIAGE_REQUEST', 'p1', { ...none, requestId: 'r1' })).toBe('/admin/requests/r1')
  })

  it('has no link for commands run in place', () => {
    for (const kind of ['START_PROJECT', 'SUBMIT_REVIEW', 'CONFIRM_APPROVAL', 'DELIVER', 'COMPLETE_PROJECT', 'COPY_PAYMENT_REMINDER'] as const)
      expect(actionHref(kind, 'p1', { ...none, deliverableId: 'd1', quotationId: 'q1' })).toBeNull()
  })
})

describe('messages for the client', () => {
  const q = {
    clientName: 'Chị Lan', projectName: 'Logo ABC', number: 'BG-2026-0007', version: 2, total: 3_000_000,
    expiryDate: '2026-11-15', paymentStatus: 'UNPAID' as const, depositPercent: 50, depositAmount: 1_500_000, outstanding: 3_000_000,
  }

  it('presents the quotation with its total and validity', () => {
    const m = quotationMessage(q)
    expect(m.startsWith('Chào Chị Lan,')).toBe(true)
    expect(m).toContain('BG-2026-0007 (v2)')
    expect(m).toContain('Logo ABC')
    expect(m).toContain(formatVnd(3_000_000))
    expect(m).toContain(formatDate('2026-11-15'))
  })

  it('asks for the deposit first', () => {
    expect(paymentReminderMessage(q)).toContain(`tiền cọc ${formatVnd(1_500_000)}`)
  })

  it('asks for the rest once the deposit is paid or when there is no deposit', () => {
    expect(paymentReminderMessage({ ...q, paymentStatus: 'DEPOSIT_PAID', outstanding: 1_500_000 }))
      .toContain(`số tiền còn lại ${formatVnd(1_500_000)}`)
    expect(paymentReminderMessage({ ...q, depositPercent: 0, depositAmount: 0 }))
      .toContain(`số tiền còn lại ${formatVnd(3_000_000)}`)
  })
})

describe('flowErrorText', () => {
  it('maps conflicts and field errors', () => {
    expect(flowErrorText({ code: 'CONCURRENCY_CONFLICT' }, 'x')).toBe('Dữ liệu đã thay đổi, vui lòng tải lại')
    expect(flowErrorText({ code: 'INVALID_TRANSITION' }, 'x')).toContain('Chuyển trạng thái không hợp lệ')
    expect(flowErrorText({ errors: { 'tasks[0].titles': ['Tên công việc dài 1–200 ký tự'] } }, 'x')).toBe('Tên công việc dài 1–200 ký tự')
    expect(flowErrorText({}, 'Không thể bắt đầu dự án')).toBe('Không thể bắt đầu dự án')
  })
})
