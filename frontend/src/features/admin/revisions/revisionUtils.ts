import type { ApiError } from '@/lib/api'
import type {
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
