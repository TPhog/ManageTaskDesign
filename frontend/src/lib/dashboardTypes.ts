import type { ProjectStatus } from './types'

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
