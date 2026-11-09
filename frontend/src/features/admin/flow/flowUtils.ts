import type { ApiError } from '@/lib/api'
import type { FlowAction, FlowActionKind, FlowStage, FlowStageState, FlowState, FlowTarget } from '@/lib/flowTypes'
import type { ProjectStatus } from '@/lib/types'
import { formatDate } from '@/lib/format'

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
      // ?new=1 opens the create dialog; a bare ?tab=deliverables is a no-op when that tab is already open
      return `${tab('deliverables')}&new=1`
    case 'VIEW_TASKS':
      return tab('tasks')
    case 'CONFIRM_BRIEF':
      return tab('brief')
    case 'CREATE_QUOTATION':
      return tab('quotations')
    case 'CREATE_ADDITIONAL_QUOTATION':
      return tab('revisions')
    case 'SEND_QUOTATION':
    case 'RECORD_ACCEPTANCE':
    case 'CONFIRM_CLIENT_ACCEPTANCE':
    case 'CONFIRM_CLIENT_PAYMENT':
      return target.quotationId ? `/admin/quotations/${target.quotationId}` : tab('quotations')
    case 'TRIAGE_REQUEST':
      return target.requestId ? `/admin/requests/${target.requestId}` : '/admin/requests'
    default:
      return null
  }
}

/** START_PROJECT / COMPLETE_PROJECT open a dialog that needs the project detail — NextStepCard owns those dialogs. */
export type FlowDialogKind = Extract<FlowActionKind, 'START_PROJECT' | 'COMPLETE_PROJECT' | 'MARK_QUOTED'>

export function isDialogAction(kind: FlowActionKind): kind is FlowDialogKind {
  return kind === 'START_PROJECT' || kind === 'COMPLETE_PROJECT' || kind === 'MARK_QUOTED'
}

/** Outside the project page (dashboard "Việc cần làm") a dialog step is a link; NextStepCard opens it from ?step=. */
export function stepHref(projectId: string, kind: FlowDialogKind): string {
  return `/admin/projects/${projectId}?step=${kind}`
}

export function stepFromParam(value: string | null): FlowDialogKind | null {
  return value === 'START_PROJECT' || value === 'COMPLETE_PROJECT' || value === 'MARK_QUOTED' ? value : null
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

/** Review / approve / deliver go through a confirmation dialog on the project page; elsewhere they are links to it. */
export function isDeliverableConfirmKind(kind: FlowActionKind): kind is 'SUBMIT_REVIEW' | 'CONFIRM_APPROVAL' | 'DELIVER' {
  return kind === 'SUBMIT_REVIEW' || kind === 'CONFIRM_APPROVAL' || kind === 'DELIVER'
}

/** An accepted but not yet started project (DRAFT/QUOTED while the flow is already at EXECUTE) reads "Sẵn sàng bắt đầu". */
export function statusBadgeOverride(
  project: { status: ProjectStatus },
  flow: { stage: FlowStageState } | undefined,
): { status: ProjectStatus; label?: string } {
  if (flow?.stage === 'EXECUTE' && (project.status === 'DRAFT' || project.status === 'QUOTED')) {
    return { status: project.status, label: 'Sẵn sàng bắt đầu' }
  }
  return { status: project.status }
}

/** Header date line: never a dangling dash. */
export function projectDateRange(start: string | null, due: string | null): string | null {
  if (start && due) return `${formatDate(start)} → ${formatDate(due)}`
  if (due) return `Deadline ${formatDate(due)}`
  if (start) return `Bắt đầu ${formatDate(start)}`
  return null
}

/** Unfinished tasks of one deliverable for the "submit for review" warning. */
export function openTaskSummary(
  tasks: { title: string; status: string; deliverableId: string | null }[],
  deliverableId: string,
  max = 5,
): { total: number; shown: string[]; more: number } {
  const open = tasks.filter(t => t.deliverableId === deliverableId && t.status !== 'DONE')
  return { total: open.length, shown: open.slice(0, max).map(t => t.title), more: Math.max(0, open.length - max) }
}

/** Empty is fine (optional field); otherwise an absolute http(s) URL without spaces, at most 500 chars. */
export function isHttpUrl(value: string): boolean {
  if (value === '') return true
  if (value.length > 500 || /\s/.test(value)) return false
  try {
    const u = new URL(value)
    return u.protocol === 'http:' || u.protocol === 'https:'
  } catch {
    return false
  }
}
