import type { ApiError } from '@/lib/api'
import type {
  AdditionalPreviewLine,
  AdditionalQuotationPreview,
  CreateAdditionalQuotationBody,
  ProjectRevisions,
  RevisionItem,
  RevisionItemInput,
  RevisionRound,
  RoundInput,
  WorkStatus,
} from '@/lib/revisionTypes'

/** Mirrors RevisionItem.ChangeWorkStatus (BR-60 diagram) */
export const workStatusTransitions: Record<WorkStatus, { to: WorkStatus; label: string }[]> = {
  TODO: [{ to: 'IN_PROGRESS', label: 'Bắt đầu làm' }, { to: 'CANCELLED', label: 'Huỷ mục' }],
  IN_PROGRESS: [{ to: 'DONE', label: 'Hoàn thành' }, { to: 'CANCELLED', label: 'Huỷ mục' }],
  DONE: [{ to: 'IN_PROGRESS', label: 'Mở lại' }],
  CANCELLED: [],
}

/** BR-46: CHARGEABLE, not on a quotation line yet, not cancelled */
export function isQuotable(item: RevisionItem): boolean {
  return item.billing === 'CHARGEABLE' && item.quotationItemId === null && item.workStatus !== 'CANCELLED'
}

export function allRounds(revisions: ProjectRevisions): RevisionRound[] {
  return [...revisions.deliverables.flatMap(d => d.rounds), ...revisions.projectRounds]
}

export function quotableItems(rounds: RevisionRound[]): RevisionItem[] {
  return rounds.flatMap(r => r.items).filter(isQuotable)
}

/** BR-60 hint; the server answers 428 SCOPE_GUARD for the same case */
export function needsScopeOverride(item: RevisionItem, to: WorkStatus): boolean {
  return (to === 'IN_PROGRESS' || to === 'DONE')
    && item.billing === 'CHARGEABLE'
    && item.quotation?.status !== 'ACCEPTED'
}

export function emptyItem(): RevisionItemInput {
  return { description: '', type: 'MINOR', billing: null }
}

export function emptyRound(deliverableId: string | null, receivedAt: string): RoundInput {
  return { deliverableId, receivedAt, items: [emptyItem()] }
}

/** Trim descriptions; drop blank items and rounds left without items before POSTing */
export function cleanRounds(rounds: RoundInput[]): RoundInput[] {
  return rounds
    .map(r => ({
      ...r,
      items: r.items
        .map(i => ({ ...i, description: i.description.trim() }))
        .filter(i => i.description.length > 0),
    }))
    .filter(r => r.items.length > 0)
}

/** Toast text for a failed command (docs/08 §4) */
export function errorText(err: ApiError, fallback: string): string {
  if (err.code === 'CONCURRENCY_CONFLICT') return 'Dữ liệu đã thay đổi ở tab khác — vui lòng tải lại'
  const fieldError = err.errors ? Object.values(err.errors).flat()[0] : undefined
  return fieldError ?? err.detail ?? fallback
}

/** Same ceiling as PUT /quotations/{id} (QuotationValidation.MaxUnitPrice). */
export const MAX_LINE_PRICE = 100_000_000_000

export type PickedService = { id: string; name: string; basePrice: number }

/** One planned line of an additional quotation with what the designer chose for it (plan 09). */
export interface PricedLine {
  line: AdditionalPreviewLine
  serviceId: string | null
  /** the server names the line after the service once one is picked */
  serviceName: string | null
  unitPrice: number
}

export const pricedLinesFromPreview = (preview: AdditionalQuotationPreview): PricedLine[] =>
  preview.lines.map(line => ({ line, serviceId: null, serviceName: null, unitPrice: line.suggestedUnitPrice }))

/** Picking prefills the base price (still editable); clearing keeps whatever price is typed. */
export const pickService = (p: PricedLine, service: PickedService | null): PricedLine =>
  service
    ? { ...p, serviceId: service.id, serviceName: service.name, unitPrice: service.basePrice }
    : { ...p, serviceId: null, serviceName: null }

/** Every shown price is sent, so the draft is exactly what the dialog showed (server: typed -> service -> suggestion). */
export function additionalQuotationBody(revisionItemIds: string[], priced: PricedLine[]): CreateAdditionalQuotationBody {
  return {
    revisionItemIds,
    newDeliverableServices: priced
      .filter(p => p.line.kind === 'NEW_DELIVERABLE')
      .map(p => ({ revisionItemId: p.line.revisionItemIds[0], serviceId: p.serviceId, unitPrice: p.unitPrice })),
    linePrices: priced
      .filter(p => p.line.kind !== 'NEW_DELIVERABLE')
      .map(p => ({ revisionItemId: p.line.revisionItemIds[0], unitPrice: p.unitPrice })),
  }
}

export const zeroPricedLineNames = (priced: PricedLine[]): string[] =>
  priced.filter(p => p.unitPrice === 0).map(p => p.serviceName ?? p.line.description)

export const linePriceError = (n: number): string | null =>
  Number.isInteger(n) && n >= 0 && n <= MAX_LINE_PRICE ? null : 'Đơn giá từ 0 đến 100.000.000.000 ₫'
