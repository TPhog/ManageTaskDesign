import type { FlowStage } from './flowTypes'
import type { ProjectStatus, TaskPriority, TaskStatus } from './types'

/** GET /api/admin/dashboard (docs/05 §3.12) */
export interface DashboardDueProject {
  id: string
  name: string
  clientName: string
  dueDate: string
  status: ProjectStatus
}

/** month: 'yyyy-MM'; total: whole VND */
export interface MonthTotal {
  month: string
  total: number
}

export interface Dashboard {
  actionRequired: {
    newRequests: number
    overdueTasks: number
    dueTodayTasks: number
    awaitingQuotations: number
    expiringSoonQuotations: number
  }
  projects: {
    inProgress: number
    onHold: number
    dueIn7Days: DashboardDueProject[]
  }
  money: {
    outstanding: number
    acceptedThisMonth: number
    additionalThisMonth: number
  }
  acceptedByMonth: MonthTotal[]
}

// ---- Plan 11 (docs/05 dashboard blocks) ----

export interface ScheduleTask {
  id: string
  title: string
  status: TaskStatus
  priority: TaskPriority
  dueDate: string
  sortOrder: number
  projectId: string
  projectName: string
  clientName: string
}

export interface ScheduleDeadline {
  kind: 'PROJECT' | 'DELIVERABLE'
  id: string
  projectId: string
  name: string
  projectName: string
  clientName: string
  date: string
}

export interface ScheduleDay {
  date: string
  tasks: ScheduleTask[]
  hiddenTasks: number
  deadlines: ScheduleDeadline[]
}

export interface DashboardSchedule {
  today: string
  undatedOpenTasks: number
  overdue: { total: number; items: ScheduleTask[] }
  days: ScheduleDay[]
}

export interface SetupItem {
  key: 'STUDIO' | 'BANK' | 'SERVICE' | 'TASK_TEMPLATE' | 'WATERMARK' | 'CLIENT'
  label: string
  hint: string
  done: boolean
  href: string
}

export interface SetupChecklist {
  dismissed: boolean
  complete: boolean
  doneCount: number
  total: number
  items: SetupItem[]
}

export type HealthRisk = 'LATE' | 'DEPOSIT_UNPAID' | 'BALANCE_DUE' | 'OPEN_REQUESTS' | 'TASKS_OVERDUE' | 'QUOTA_EXCEEDED'

export interface ProjectHealthItem {
  projectId: string
  projectName: string
  clientId: string
  clientName: string
  status: ProjectStatus
  stage: FlowStage
  dueDate: string | null
  daysToDeadline: number | null
  progressPercent: number | null
  quotaUsed: number
  quotaTotal: number
  risks: HealthRisk[]
  riskScore: number
}

export interface ProjectHealth {
  total: number
  hidden: number
  items: ProjectHealthItem[]
}

export type MoneyPeriod = 'month' | 'quarter' | 'year'

export interface DateWindow {
  start: string
  endExclusive: string
}

export interface ReceivableItem {
  quotationId: string
  number: string | null
  version: number
  kind: 'INITIAL' | 'ADDITIONAL'
  clientId: string
  clientName: string
  projectId: string
  projectName: string
  paymentStatus: 'UNPAID' | 'DEPOSIT_PAID'
  total: number
  depositPercent: number
  depositAmount: number
  outstanding: number
  agingFrom: string
  agingDays: number
  agingBucket: '0-7' | '8-30' | '31+'
}

export interface ReceivableClient {
  clientId: string
  clientName: string
  outstanding: number
  count: number
}

export interface MoneyInsights {
  period: MoneyPeriod
  periodStart: string
  periodEnd: string
  current: DateWindow
  previous: DateWindow
  collected: { current: number; previous: number; deltaPercent: number | null; currentDeposits: number; currentRemainders: number }
  accepted: { current: number; previous: number; deltaPercent: number | null }
  receivables: { total: number; items: ReceivableItem[]; byClient: ReceivableClient[]; hiddenItems: number }
}

export type ClientActivityKind =
  | 'REQUEST_CREATED' | 'DELIVERABLE_APPROVED' | 'QUOTATION_ACCEPTED'
  | 'QUOTATION_CLIENT_ACCEPTED' | 'CLIENT_PAYMENT_NOTICE' | 'INTAKE_SUBMITTED'

export interface ClientActivityItem {
  id: number
  occurredAt: string
  kind: ClientActivityKind
  text: string
  clientName: string | null
  projectName: string | null
  href: string
}

export interface ClientActivity {
  items: ClientActivityItem[]
}
