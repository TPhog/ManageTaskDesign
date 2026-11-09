import {
  actionHref,
  flowActionKey,
  flowErrorText,
  isCopyAction,
  isDialogAction,
  stageLabel,
  stepFromParam,
  stepHref,
  stepperSummary,
} from '@/features/admin/flow/flowUtils'

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
    expect(actionHref('CONFIRM_CLIENT_ACCEPTANCE', 'p1', { ...none, quotationId: 'q1' })).toBe('/admin/quotations/q1')
    expect(actionHref('CONFIRM_CLIENT_PAYMENT', 'p1', { ...none, quotationId: 'q1' })).toBe('/admin/quotations/q1')
  })

  it('has no link for commands run in place', () => {
    for (const kind of ['START_PROJECT', 'SUBMIT_REVIEW', 'CONFIRM_APPROVAL', 'DELIVER', 'COMPLETE_PROJECT', 'COPY_PAYMENT_REMINDER'] as const)
      expect(actionHref(kind, 'p1', { ...none, deliverableId: 'd1', quotationId: 'q1' })).toBeNull()
  })
})

describe('dialog steps and action helpers', () => {
  it('knows which actions open a dialog on the project page', () => {
    expect(isDialogAction('START_PROJECT')).toBe(true)
    expect(isDialogAction('COMPLETE_PROJECT')).toBe(true)
    expect(isDialogAction('DELIVER')).toBe(false)
  })

  it('deep-links a dialog step to the project page', () => {
    expect(stepHref('p1', 'START_PROJECT')).toBe('/admin/projects/p1?step=START_PROJECT')
  })

  it('reads only known steps from the URL', () => {
    expect(stepFromParam('COMPLETE_PROJECT')).toBe('COMPLETE_PROJECT')
    expect(stepFromParam('DELIVER')).toBeNull()
    expect(stepFromParam(null)).toBeNull()
  })

  it('marks copy actions and keys actions by their target', () => {
    expect(isCopyAction('COPY_PAYMENT_REMINDER')).toBe(true)
    expect(isCopyAction('SEND_QUOTATION')).toBe(false)
    expect(flowActionKey({ kind: 'SUBMIT_REVIEW', label: 'x', primary: false, target: { ...none, deliverableId: 'd1' } }))
      .toBe('SUBMIT_REVIEW-d1')
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
