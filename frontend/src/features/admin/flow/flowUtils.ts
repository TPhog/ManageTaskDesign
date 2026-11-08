import type { ApiError } from '@/lib/api'
import { formatDate, formatVnd } from '@/lib/format'
import type { FlowActionKind, FlowStage, FlowStageState, FlowState, FlowTarget } from '@/lib/flowTypes'
import type { QuotationDetail } from '@/lib/quotationTypes'

export const FLOW_STAGES: FlowStage[] = ['PREPARE', 'QUOTE', 'EXECUTE', 'REVIEW', 'CLOSE']

export const stageLabel: Record<FlowStageState, string> = {
  PREPARE: 'Chuẩn bị',
  QUOTE: 'Báo giá',
  EXECUTE: 'Thực hiện',
  REVIEW: 'Duyệt & bàn giao',
  CLOSE: 'Hoàn tất',
  CANCELLED: 'Đã huỷ',
}

/** Mobile stepper line: "Giai đoạn 3/5 · Thực hiện". */
export function stepperSummary(flow: Pick<FlowState, 'stage'>): string {
  if (flow.stage === 'CANCELLED') return 'Dự án đã huỷ'
  return `Giai đoạn ${FLOW_STAGES.indexOf(flow.stage) + 1}/${FLOW_STAGES.length} · ${stageLabel[flow.stage]}`
}

/** Actions that open another screen; null = a command run in place (NextStepCard handles it). */
export function actionHref(kind: FlowActionKind, projectId: string, target: FlowTarget): string | null {
  const tab = (name: string) => `/admin/projects/${projectId}?tab=${name}`
  switch (kind) {
    case 'ADD_DELIVERABLE':
      return tab('deliverables')
    case 'CONFIRM_BRIEF':
      return tab('brief')
    case 'CREATE_QUOTATION':
      return tab('quotations')
    case 'CREATE_ADDITIONAL_QUOTATION':
      return tab('revisions')
    case 'SEND_QUOTATION':
    case 'RECORD_ACCEPTANCE':
      return target.quotationId ? `/admin/quotations/${target.quotationId}` : tab('quotations')
    case 'TRIAGE_REQUEST':
      return target.requestId ? `/admin/requests/${target.requestId}` : '/admin/requests'
    default:
      return null
  }
}

type QuotationRef = Pick<QuotationDetail, 'number' | 'version'>

const quotationRef = (q: QuotationRef) => (q.number ? ` ${q.number} (v${q.version})` : '')

/** COPY_QUOTATION_MESSAGE: text the designer pastes into Zalo. */
export function quotationMessage(
  q: QuotationRef & Pick<QuotationDetail, 'clientName' | 'projectName' | 'total' | 'expiryDate'>,
): string {
  return [
    `Chào ${q.clientName},`,
    `Mình gửi bạn báo giá${quotationRef(q)} cho dự án ${q.projectName}, tổng ${formatVnd(q.total)}.`,
    q.expiryDate ? `Báo giá có hiệu lực đến ${formatDate(q.expiryDate)}.` : null,
    'Bạn xem chi tiết và mã QR thanh toán trong trang theo dõi dự án nhé. Cảm ơn bạn!',
  ]
    .filter((line): line is string => line !== null)
    .join('\n')
}

/** COPY_PAYMENT_REMINDER (F4): the deposit while nothing is paid, otherwise what is still due (BR-73). */
export function paymentReminderMessage(
  q: QuotationRef &
    Pick<QuotationDetail, 'clientName' | 'projectName' | 'paymentStatus' | 'depositPercent' | 'depositAmount' | 'outstanding'>,
): string {
  const deposit = q.paymentStatus === 'UNPAID' && q.depositPercent > 0
  const what = deposit ? `tiền cọc ${formatVnd(q.depositAmount)}` : `số tiền còn lại ${formatVnd(q.outstanding)}`
  return [
    `Chào ${q.clientName},`,
    `Mình nhắc nhẹ ${what} của báo giá${quotationRef(q)} (dự án ${q.projectName}).`,
    'Bạn quét mã QR trong trang theo dõi dự án hoặc chuyển khoản theo thông tin trên báo giá giúp mình nhé. Cảm ơn bạn!',
  ].join('\n')
}

export function flowErrorText(err: Pick<ApiError, 'code' | 'detail' | 'errors'>, fallback: string): string {
  if (err.code === 'CONCURRENCY_CONFLICT') return 'Dữ liệu đã thay đổi, vui lòng tải lại'
  if (err.code === 'INVALID_TRANSITION') return 'Chuyển trạng thái không hợp lệ — tải lại trang để xem trạng thái mới'
  const fieldError = err.errors ? Object.values(err.errors).flat()[0] : undefined
  return fieldError ?? err.detail ?? fallback
}
