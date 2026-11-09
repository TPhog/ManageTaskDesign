import type { PortalImage } from './imageTypes'
import type { DeliverableStatus, DesignType, ProjectStatus } from './types'
import type {
  EffectiveStatus,
  PaymentStatus,
  QrOption,
  QrPurpose,
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
    /** Plan 12 CF-13: "Nhà thiết kế thường phản hồi {responseTime}" — e.g. "trong 24 giờ" */
    responseTime: string
    /** P13: public intake form URL; null when the designer turned the form off (absent on older backends) */
    intakeUrl?: string | null
  }
}

/** Plan 12 BR-48: the one review state the client sees for a deliverable. */
export type PortalReviewState = 'NONE' | 'AWAITING_CLIENT' | 'CLIENT_APPROVED' | 'CHANGES_IN_PROGRESS'

/** Plan 12 BR-77: what the client reported with "Tôi đã chuyển khoản"; pending = the designer has not confirmed it yet. */
export interface PortalPaymentNotice {
  purpose: QrPurpose
  amount: number
  at: string
  note: string | null
  pending: boolean
}

export interface PortalDeliverableSummary {
  /** Excludes CANCELLED */
  total: number
  delivered: number
  approved: number
  /** IN_REVIEW — raw status (QA C-06) */
  inReview: number
  /** Plan 12 BR-48: reviewState = AWAITING_CLIENT — what the client still has to review */
  awaitingClient: number
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
  /** plan 06b: % of the designer's tasks that are DONE; null = no task yet */
  progressPercent: number | null
  /** 'APPROVED' after the client pressed "Duyệt" (still IN_REVIEW until the designer confirms) */
  clientDecision: 'APPROVED' | null
  /** Plan 12 BR-48 */
  reviewState: PortalReviewState
  /** a NEW request or an unfinished revision item is open for this deliverable */
  hasOpenChangeRequest: boolean
  /** P15: only the links the designer marked "khách xem được" (no flag). Optional so older fixtures keep compiling */
  links?: { title: string; url: string }[]
}

/** POST /api/portal/deliverables/{id}/approve */
export interface PortalDeliverableDecision {
  id: string
  status: DeliverableStatus
  clientDecision: 'APPROVED' | null
  clientDecisionAt: string | null
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
  /** MESSAGE = the designer's progress update, EVENT = an automatic status line (QA C-12) */
  kind: 'MESSAGE' | 'EVENT'
  /** progress-update photos (preview/thumb only); [] otherwise */
  images: PortalImage[]
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
  /** Plan 12: the client pressed "Đồng ý báo giá" (status stays SENT until the designer confirms) */
  clientAcceptedAt: string | null
  clientPaymentNotice: PortalPaymentNotice | null
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
  clientAcceptedAt: string | null
  clientPaymentNotice: PortalPaymentNotice | null
}

export type PortalRequestStatus = 'NEW' | 'TRIAGED' | 'DECLINED'

export interface PortalRequestAttachment {
  id: string
  originalName: string
  contentType: string
  thumbUrl: string | null
  previewUrl: string | null
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
  /** plan 13: name of the template used, null for free text */
  templateName: string | null
}

export type PortalBilling = 'FREE_FIX' | 'IN_QUOTA' | 'CHARGEABLE'
export type PortalWorkStatus = 'TODO' | 'IN_PROGRESS' | 'DONE' | 'CANCELLED'

/** GET /api/portal/projects/{id}/revisions — no internal notes, scope overrides or quotation links (REV-07). */
export interface PortalRevisionItem {
  id: string
  description: string
  type: string
  billing: PortalBilling
  workStatus: PortalWorkStatus
}

export interface PortalRound {
  id: string
  roundNo: number
  receivedAt: string
  items: PortalRevisionItem[]
}

export interface PortalDeliverableRevisions {
  id: string
  name: string
  includedRounds: number
  usedRounds: number
  remainingRounds: number
  rounds: PortalRound[]
}

export interface PortalRevisions {
  deliverables: PortalDeliverableRevisions[]
  /** rounds not tied to a deliverable (never IN_QUOTA — BR-41) */
  projectRounds: PortalRound[]
}

/** GET /api/portal/studio — public, for pages without a portal session */
export interface PortalStudio {
  displayName: string
  contactPhone: string | null
  contactEmail: string | null
}
