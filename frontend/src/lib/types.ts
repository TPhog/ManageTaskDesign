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
}

/** POST /admin/clients/{id}/portal-link — the URL is returned only this once (07-security §4.1). */
export interface PortalLinkResult {
  url: string
  createdAt: string
}
