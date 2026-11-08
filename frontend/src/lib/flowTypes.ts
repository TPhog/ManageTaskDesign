// Mirrors backend Domain/Flow/ProjectFlow.cs and Features/Projects/ProjectFlowEndpoints.cs (plan 06b).

export type FlowStage = 'PREPARE' | 'QUOTE' | 'EXECUTE' | 'REVIEW' | 'CLOSE'
/** CANCELLED is never a step: it is the stage reported for a cancelled project. */
export type FlowStageState = FlowStage | 'CANCELLED'
export type FlowStepStatus = 'done' | 'current' | 'upcoming'

export type FlowActionKind =
  | 'ADD_DELIVERABLE'
  | 'CONFIRM_BRIEF'
  | 'CREATE_QUOTATION'
  | 'SEND_QUOTATION'
  | 'COPY_QUOTATION_MESSAGE'
  | 'RECORD_ACCEPTANCE'
  | 'START_PROJECT'
  | 'SUBMIT_REVIEW'
  | 'CONFIRM_APPROVAL'
  | 'TRIAGE_REQUEST'
  | 'DELIVER'
  | 'CREATE_ADDITIONAL_QUOTATION'
  | 'COMPLETE_PROJECT'
  | 'COPY_PAYMENT_REMINDER'

export type FlowWarningKind =
  | 'BRIEF_NOT_CONFIRMED'
  | 'QUOTATION_EXPIRING'
  | 'QUOTATION_EXPIRED'
  | 'DEPOSIT_UNPAID'
  | 'TASKS_OVERDUE'
  | 'QUOTA_EXCEEDED'
  | 'BALANCE_DUE'
  | 'DELIVERED_WITH_OPEN_REVISIONS'

export interface FlowTarget {
  deliverableId: string | null
  quotationId: string | null
  requestId: string | null
}

export interface FlowAction {
  kind: FlowActionKind
  /** Vietnamese, ready to show */
  label: string
  /** exactly one action (the first) is primary */
  primary: boolean
  target: FlowTarget
}

export interface FlowWarning {
  kind: FlowWarningKind
  message: string
  severity: 'warning' | 'info'
}

export interface FlowStep {
  stage: FlowStage
  status: FlowStepStatus
}

/** GET /admin/projects/{id}/flow */
export interface FlowState {
  stage: FlowStageState
  /** always the 5 stages in order */
  steps: FlowStep[]
  actions: FlowAction[]
  warnings: FlowWarning[]
}

/** GET /admin/flow/actions item (dashboard, plan 07) */
export interface FlowProjectActions {
  projectId: string
  projectName: string
  clientName: string
  stage: FlowStageState
  actions: FlowAction[]
  warnings: FlowWarning[]
}

/** GET /admin/projects/{id}/start-preview item — NOT_STARTED deliverables with their service template */
export interface StartPreviewItem {
  deliverableId: string
  name: string
  serviceName: string | null
  titles: string[]
}

export interface StartProjectBody {
  rowVersion: number
  tasks: { deliverableId: string; titles: string[] }[]
}

export interface StartProjectResult {
  projectId: string
  status: 'IN_PROGRESS'
  startedDeliverables: number
  createdTasks: number
  /** never blocking (F4) */
  warnings: string[]
}
