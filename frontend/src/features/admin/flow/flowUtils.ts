import type { ApiError } from '@/lib/api'
import type { FlowAction, FlowActionKind, FlowStage, FlowStageState, FlowState, FlowTarget } from '@/lib/flowTypes'

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

/** START_PROJECT / COMPLETE_PROJECT open a dialog that needs the project detail — NextStepCard owns those dialogs. */
export type FlowDialogKind = Extract<FlowActionKind, 'START_PROJECT' | 'COMPLETE_PROJECT'>

export function isDialogAction(kind: FlowActionKind): kind is FlowDialogKind {
  return kind === 'START_PROJECT' || kind === 'COMPLETE_PROJECT'
}

/** Outside the project page (dashboard "Việc cần làm") a dialog step is a link; NextStepCard opens it from ?step=. */
export function stepHref(projectId: string, kind: FlowDialogKind): string {
  return `/admin/projects/${projectId}?step=${kind}`
}

export function stepFromParam(value: string | null): FlowDialogKind | null {
  return value === 'START_PROJECT' || value === 'COMPLETE_PROJECT' ? value : null
}

export function isCopyAction(kind: FlowActionKind): boolean {
  return kind === 'COPY_QUOTATION_MESSAGE' || kind === 'COPY_PAYMENT_REMINDER'
}

/** Stable React key: one action kind per target. */
export function flowActionKey(a: FlowAction): string {
  return `${a.kind}-${a.target.deliverableId ?? a.target.quotationId ?? a.target.requestId ?? ''}`
}

export function flowErrorText(err: Pick<ApiError, 'code' | 'detail' | 'errors'>, fallback: string): string {
  if (err.code === 'CONCURRENCY_CONFLICT') return 'Dữ liệu đã thay đổi, vui lòng tải lại'
  if (err.code === 'INVALID_TRANSITION') return 'Chuyển trạng thái không hợp lệ — tải lại trang để xem trạng thái mới'
  const fieldError = err.errors ? Object.values(err.errors).flat()[0] : undefined
  return fieldError ?? err.detail ?? fallback
}
