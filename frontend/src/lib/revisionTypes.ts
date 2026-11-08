// docs/05 §3.7–3.8 — admin requests & revisions API. Dates 'YYYY-MM-DD'; instants ISO-8601 UTC.
import type { EffectiveStatus, QuotationStatus } from './quotationTypes'
import type { DeliverableStatus, TaskPriority, TaskStatus } from './types'

export type RequestStatus = 'NEW' | 'TRIAGED' | 'DECLINED'
export type RequestSource = 'PORTAL' | 'ZALO' | 'EMAIL' | 'PHONE' | 'OTHER'
/** REQ-02: PORTAL is reserved for requests the client sends */
export type DesignerRequestSource = Exclude<RequestSource, 'PORTAL'>
export type RevisionItemType = 'MINOR' | 'MAJOR' | 'SCOPE_CHANGE' | 'NEW_DELIVERABLE'
export type Billing = 'FREE_FIX' | 'IN_QUOTA' | 'CHARGEABLE'
export type WorkStatus = 'TODO' | 'IN_PROGRESS' | 'DONE' | 'CANCELLED'

export interface RequestAttachment {
  id: string
  originalName: string
  contentType: string
  /** null for legacy non-image files */
  thumbUrl: string | null
  previewUrl: string | null
  originalUrl: string | null
}

export interface ClientRequest {
  id: string
  projectId: string
  projectName: string
  clientId: string
  clientName: string
  deliverableId: string | null
  deliverableName: string | null
  content: string
  source: RequestSource
  createdBy: 'CLIENT' | 'DESIGNER'
  status: RequestStatus
  declineReason: string | null
  triagedAt: string | null
  createdAt: string
  rowVersion: number
  attachments: RequestAttachment[]
}

export interface LinkedQuotation {
  id: string
  number: string | null
  version: number
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
}

export interface LinkedTask {
  id: string
  title: string
  status: TaskStatus
}

export interface RevisionItem {
  id: string
  roundId: string
  description: string
  type: RevisionItemType
  billing: Billing
  workStatus: WorkStatus
  quotationItemId: string | null
  quotation: LinkedQuotation | null
  scopeOverrideReason: string | null
  internalNotes: string
  sortOrder: number
  rowVersion: number
  /** Only in the response that created the item */
  suggestedBilling: Billing | null
  suggestionReason: string | null
  tasks: LinkedTask[]
}

export interface RevisionRound {
  id: string
  projectId: string
  deliverableId: string | null
  deliverableName: string | null
  roundNo: number
  receivedAt: string
  sourceRequestId: string | null
  /** Uses one quota round (BR-42) */
  counted: boolean
  createdAt: string
  items: RevisionItem[]
}

export interface DeliverableRevisions {
  id: string
  name: string
  status: DeliverableStatus
  includedRounds: number
  usedRounds: number
  remainingRounds: number
  extraRoundPrice: number
  rounds: RevisionRound[]
}

export interface ProjectRevisions {
  projectId: string
  deliverables: DeliverableRevisions[]
  projectRounds: RevisionRound[]
}

export interface RequestDetail {
  request: ClientRequest
  rounds: RevisionRound[]
}

export interface BillingSuggestion {
  billing: Billing
  reason: string
}

/** One item of a round being composed; billing null = let the server apply its suggestion */
export interface RevisionItemInput {
  description: string
  type: RevisionItemType
  billing: Billing | null
}

export interface RoundInput {
  deliverableId: string | null
  receivedAt: string | null
  items: RevisionItemInput[]
}

export interface CreateRequestBody {
  content: string
  source: DesignerRequestSource
  deliverableId: string | null
}

export interface TriageBody {
  rounds: RoundInput[]
  rowVersion: number
}

export interface UpdateRevisionItemBody {
  description: string
  type: RevisionItemType
  billing: Billing
  internalNotes: string
  rowVersion: number
}

export interface WorkStatusBody {
  to: WorkStatus
  overrideReason: string | null
  rowVersion: number
}

export interface CreateItemTaskBody {
  title?: string
  priority?: TaskPriority
  dueDate?: string | null
}

export interface CreateAdditionalQuotationBody {
  title?: string
  revisionItemIds: string[]
  newDeliverableServices?: { revisionItemId: string; serviceId: string }[]
}

/** GET /admin/projects/{id}/activity item */
export interface ActivityEntry {
  id: number
  occurredAt: string
  actorType: string
  entityType: string
  entityId: string
  action: string
  data: string
  visibleToClient: boolean
  clientMessage: string | null
}
