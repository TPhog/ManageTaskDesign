// docs/05 §3.10 — admin quotation API. Money: whole VND numbers. Dates: 'YYYY-MM-DD'.
export type QuotationStatus = 'DRAFT' | 'SENT' | 'ACCEPTED' | 'REJECTED' | 'SUPERSEDED'
/** EXPIRED is computed on read (SENT past expiry), never stored. */
export type EffectiveStatus = QuotationStatus | 'EXPIRED'
export type PaymentStatus = 'UNPAID' | 'DEPOSIT_PAID' | 'PAID'
export type QuotationKind = 'INITIAL' | 'ADDITIONAL'
export type QuotationItemSource = 'MANUAL' | 'SERVICE' | 'DELIVERABLE' | 'REVISION'
export type QrPurpose = 'deposit' | 'full' | 'remaining'

export interface QuotationListItem {
  id: string
  projectId: string
  projectName: string
  clientName: string
  kind: QuotationKind
  number: string | null
  version: number
  title: string
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
  paymentStatus: PaymentStatus
  total: number
  depositAmount: number
  outstanding: number
  issueDate: string | null
  expiryDate: string | null
  createdAt: string
}

export interface QuotationItem {
  id: string
  description: string
  details: string
  quantity: number
  unitPrice: number
  amount: number
  sourceType: QuotationItemSource
  sourceId: string | null
  sortOrder: number
}

export interface SnapshotBank {
  bin: string
  shortName: string
  accountNo: string
  accountName: string
}

export interface SnapshotItem {
  description: string
  details: string
  quantity: number
  unitPrice: number
  amount: number
}

/** quotations.snapshot (docs/04 §5) — the only display source of a non-DRAFT quotation (BR-39). */
export interface QuotationSnapshot {
  schemaVersion: number
  number: string
  version: number
  kind: QuotationKind
  title: string
  issueDate: string
  expiryDate: string
  designer: { displayName: string; email: string | null; phone: string | null }
  client: { name: string; company: string | null }
  project: { id: string; name: string }
  items: SnapshotItem[]
  subtotal: number
  discountAmount: number
  vatPercent: number
  vatAmount: number
  total: number
  depositPercent: number
  depositAmount: number
  terms: string
  notesToClient: string
  bank: SnapshotBank
}

export interface QrOption {
  purpose: QrPurpose
  label: string
  amount: number
  memo: string
  /** EMVCo VietQR payload, rendered client-side */
  payload: string
}

export interface QuotationVersion {
  id: string
  version: number
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
}

/** Plan 09: a NEW_DELIVERABLE revision item on a line of this quotation (BR-48). itemDescription = the name the deliverable gets. */
export interface QuotationNewDeliverable {
  revisionItemId: string
  quotationItemId: string
  lineDescription: string
  serviceId: string | null
  serviceName: string | null
  createdDeliverableId: string | null
  createdDeliverableName: string | null
  itemDescription: string
}

/** Plan 12: what the client reported on the portal with "Tôi đã chuyển khoản" (recorded only — BR-01). */
export interface ClientPaymentNotice {
  purpose: QrPurpose
  amount: number
  at: string
  note: string | null
  pending: boolean
}

export interface QuotationDetail {
  id: string
  projectId: string
  projectName: string
  clientName: string
  kind: QuotationKind
  number: string | null
  version: number
  previousVersionId: string | null
  status: QuotationStatus
  effectiveStatus: EffectiveStatus
  paymentStatus: PaymentStatus
  rowVersion: number
  title: string
  validityDays: number
  discountAmount: number
  vatPercent: number
  depositPercent: number
  terms: string
  notesToClient: string
  internalNotes: string
  subtotal: number
  vatAmount: number
  total: number
  depositAmount: number
  outstanding: number
  issueDate: string | null
  expiryDate: string | null
  sentAt: string | null
  acceptedAt: string | null
  acceptedNote: string | null
  rejectedAt: string | null
  rejectedReason: string | null
  depositPaidAt: string | null
  paidAt: string | null
  /** Plan 12: the client pressed "Đồng ý báo giá" on the portal */
  clientAcceptedAt: string | null
  clientPaymentNotice: ClientPaymentNotice | null
  /** DRAFT only — every other status renders from snapshot */
  items: QuotationItem[]
  snapshot: QuotationSnapshot | null
  qrOptions: QrOption[]
  versions: QuotationVersion[]
  /** plan 09; optional so older fixtures keep compiling */
  newDeliverables?: QuotationNewDeliverable[]
}

/** D1: deliverableIds = only those finished deliverables; omitted = whole project. */
export interface CreateQuotationBody {
  kind: 'INITIAL'
  title: string | null
  fromDeliverables: boolean
  deliverableIds?: string[]
  /** plan 09: prices for deliverables without a service */
  deliverablePrices?: { deliverableId: string; unitPrice: number }[]
}

export interface CreateQuotationResult {
  id: string
  warnings: string[]
}

export interface QuotationItemInput {
  id: string | null
  description: string
  details: string
  quantity: number
  unitPrice: number
  sourceType: QuotationItemSource
  sourceId: string | null
}

export interface UpdateQuotationBody {
  title: string
  validityDays: number
  discountAmount: number
  vatPercent: number
  depositPercent: number
  terms: string
  notesToClient: string
  internalNotes: string
  items: QuotationItemInput[]
  rowVersion: number
}

export interface QuotationListParams {
  projectId?: string
  clientId?: string
  effectiveStatus?: string
  paymentStatus?: string
  page?: number
  pageSize?: number
}
