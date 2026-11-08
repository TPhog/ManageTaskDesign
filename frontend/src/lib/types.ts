export type ProjectStatus = 'DRAFT' | 'QUOTED' | 'IN_PROGRESS' | 'ON_HOLD' | 'COMPLETED' | 'CANCELLED'
export type DesignType = 'LOGO' | 'BANNER' | 'POSTER' | 'BRANDING' | 'UI_UX' | 'SOCIAL_MEDIA' | 'PACKAGING' | 'PRINT' | 'OTHER'
export type DeliverableStatus = 'NOT_STARTED' | 'IN_PROGRESS' | 'IN_REVIEW' | 'APPROVED' | 'DELIVERED' | 'CANCELLED'
export type BriefStatus = 'DRAFT' | 'CONFIRMED'

export interface ClientRef {
  id: string
  name: string
  company: string | null
}

export interface ProjectListItem {
  id: string
  name: string
  client: ClientRef
  designType: DesignType
  status: ProjectStatus
  startDate: string | null
  dueDate: string | null
  deliverableCount: number
  createdAt: string
}

/** GET /admin/projects/{id} deliverables[].quotedIn - live quotations already containing the deliverable (QA D-02) */
export interface DeliverableQuoteRef {
  quotationId: string
  number: string | null
  version: number
  status: 'DRAFT' | 'SENT' | 'ACCEPTED'
  kind: 'INITIAL' | 'ADDITIONAL'
}

export interface Deliverable {
  id: string
  name: string
  quantity: number
  serviceId: string | null
  includedRounds: number
  extraRoundPrice: number
  usedRounds: number
  remainingRounds: number
  status: DeliverableStatus
  dueDate: string | null
  sortOrder: number
  /** Plan 06c Task 1; optional so older fixtures keep compiling */
  quotedIn?: DeliverableQuoteRef[]
  /** plan 06b: tasks linked to the deliverable (DONE / all) */
  taskTotal: number
  taskDone: number
  /** % of tasks DONE, half-up; null when the deliverable has no task */
  progressPercent: number | null
  /** 'APPROVED' once the client pressed "Duyệt" on the portal; cleared when it leaves IN_REVIEW */
  clientDecision: 'APPROVED' | null
  clientDecisionAt: string | null
}

export interface Brief {
  id: string
  version: number
  status: BriefStatus
  content: string
  confirmedAt: string | null
  createdAt: string
  updatedAt: string
}

export interface ProjectDetail {
  id: string
  name: string
  client: ClientRef
  designType: DesignType
  status: ProjectStatus
  statusReason: string | null
  statusReasonPublic: boolean
  startDate: string | null
  dueDate: string | null
  internalNotes: string
  deliverables: Deliverable[]
  brief: Brief | null
  rowVersion: number
  createdAt: string
  updatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface ClientListItem {
  id: string
  name: string
  company: string | null
  phone: string | null
  status: 'ACTIVE' | 'INACTIVE'
  projectCount: number
  portalEnabled: boolean
  acceptedTotal: number
  /** BR-73 */
  outstanding: number
}

export interface ClientDetail {
  id: string
  name: string
  company: string | null
  phone: string | null
  email: string | null
  contactChannel: string | null
  status: 'ACTIVE' | 'INACTIVE'
  portalEnabled: boolean
  hasPortalLink: boolean
  portalTokenVersion: number
  portalTokenCreatedAt: string | null
  archivedAt: string | null
  projectCount: number
  /** internal - never shown to the client */
  notes: string
  acceptedTotal: number
  outstanding: number
  pendingQuotationTotal: number
  newRequests: number
  rowVersion: number
  createdAt: string
}

/** POST /admin/clients/{id}/portal-link — the URL is returned only this once (07-security §4.1). */
export interface PortalLinkResult {
  url: string
  createdAt: string
}

export type TaskStatus = 'TODO' | 'IN_PROGRESS' | 'REVIEW' | 'DONE' | 'BLOCKED'
export type TaskPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT'
/** GET /admin/tasks?due= — server buckets by today(VN) and skips DONE tasks */
export type TaskDueFilter = 'overdue' | 'today' | 'week'

/** GET /admin/tasks item */
export interface TaskListItem {
  id: string
  projectId: string
  projectName: string
  clientName: string
  deliverableId: string | null
  deliverableName: string | null
  title: string
  status: TaskStatus
  priority: TaskPriority
  startDate: string | null
  dueDate: string | null
  sortOrder: number
  completedAt: string | null
  isOverdue: boolean
  checklistTotal: number
  checklistDone: number
  createdAt: string
  updatedAt: string
  hasDescription: boolean
  notesCount: number
}

export interface ChecklistItem {
  id: string
  text: string
  done: boolean
  sortOrder: number
  description: string
}

/** GET /admin/tasks/{id}; every task write returns this shape */
export interface TaskDetail {
  id: string
  projectId: string
  projectName: string
  deliverableId: string | null
  deliverableName: string | null
  title: string
  description: string
  status: TaskStatus
  priority: TaskPriority
  startDate: string | null
  dueDate: string | null
  sortOrder: number
  completedAt: string | null
  isOverdue: boolean
  checklist: ChecklistItem[]
  createdAt: string
  updatedAt: string
  /** Set when the task was created from a revision item */
  revisionItemId?: string | null
  notesCount: number
}

/** GET /admin/tasks/{id}/notes item — designer-only */
export interface TaskNote {
  id: string
  body: string
  createdAt: string
  updatedAt: string
}

export interface TaskListParams {
  projectId?: string
  status?: TaskStatus
  due?: TaskDueFilter
  /** title or project name (Plan 06c Task 1) */
  q?: string
  clientId?: string
  page?: number
  /** default 100 (Kanban loads a whole project) */
  pageSize?: number
}

/** PUT /admin/tasks/{id} body */
export interface TaskUpdateInput {
  title: string
  description: string
  status: TaskStatus
  priority: TaskPriority
  startDate: string | null
  dueDate: string | null
  deliverableId: string | null
}

/** GET/PUT /admin/settings/digest: daily Discord digest on/off + VN hour */
export interface DigestSettings {
  enabled: boolean
  hourVn: number
  channelConfigured: boolean
}
