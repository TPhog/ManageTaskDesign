import type { DeliverableStatus, DesignType, ProjectStatus } from './types'
import type {
  EffectiveStatus,
  PaymentStatus,
  QrOption,
  QuotationKind,
  QuotationSnapshot,
  QuotationStatus,
} from './quotationTypes'

// Mirrors backend Features/Portal/PortalDtos.cs — portal DTOs never carry internal fields.

export interface PortalMe {
  clientName: string
  designer: {
    displayName: string
    logoUrl: string | null
    contactPhone: string | null
    contactEmail: string | null
  }
}

export interface PortalDeliverableSummary {
  /** Excludes CANCELLED */
  total: number
  delivered: number
  approved: number
}

export interface PortalProjectListItem {
  id: string
  name: string
  designType: DesignType
  status: ProjectStatus
  dueDate: string | null
  deliverableSummary: PortalDeliverableSummary
  hasOpenQuotation: boolean
  /** Newest SENT, not expired quotation — target of the "Có báo giá mới" chip */
  openQuotationId: string | null
}

export interface PortalDeliverable {
  id: string
  name: string
  quantity: number
  status: DeliverableStatus
  includedRounds: number
  usedRounds: number
  remainingRounds: number
  dueDate: string | null
}

export interface PortalBrief {
  version: number
  content: string
  confirmedAt: string | null
}

export interface PortalProjectDetail {
  id: string
  name: string
  designType: DesignType
  status: ProjectStatus
  /** Only present when the designer made the reason public */
  statusReason: string | null
  startDate: string | null
  dueDate: string | null
  deliverableSummary: PortalDeliverableSummary
  deliverables: PortalDeliverable[]
  brief: PortalBrief | null
}

export interface PortalTimelineItem {
  occurredAt: string
  message: string
}

/** GET /api/portal/quotations — every figure comes from the snapshot (BR-39); never DRAFT or SUPERSEDED. */
export interface PortalQuotationListItem {
  id: string
  projectId: string
  projectName: string
  number: string
  version: number
  kind: QuotationKind
  title: string
  status: QuotationStatus
  /** EXPIRED = SENT past expiry, computed on read */
  effectiveStatus: EffectiveStatus
  paymentStatus: PaymentStatus
  total: number
  depositAmount: number
  issueDate: string
  expiryDate: string
}

export interface PortalQuotationVersion {
  version: number
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
}

/** GET /api/portal/quotations/{id} and …/versions/{version} */
export interface PortalQuotationDetail {
  id: string
  projectId: string
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
  paymentStatus: PaymentStatus
  snapshot: QuotationSnapshot
  /** BR-75 options with a ready-to-render VietQR payload; empty when nothing is payable */
  qrOptions: QrOption[]
  /** non-DRAFT versions of the chain, oldest first */
  versions: PortalQuotationVersion[]
}

export type PortalRequestStatus = 'NEW' | 'TRIAGED' | 'DECLINED'

export interface PortalRequestAttachment {
  id: string
  originalName: string
  contentType: string
}

/** GET /api/portal/requests — the client's own requests, newest first */
export interface PortalRequest {
  id: string
  projectId: string
  projectName: string
  deliverableId: string | null
  deliverableName: string | null
  createdAt: string
  content: string
  status: PortalRequestStatus
  /** set when DECLINED (BR-52) */
  declineReason: string | null
  attachments: PortalRequestAttachment[]
}
