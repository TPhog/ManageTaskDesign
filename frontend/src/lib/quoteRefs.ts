import type { DeliverableQuoteRef } from './types'

const statusWord: Record<DeliverableQuoteRef['status'], string> = {
  DRAFT: 'nháp',
  SENT: 'đã gửi',
  ACCEPTED: 'đã chấp nhận',
}

/** 'BG-2026-0028 v2 · đã gửi'; a first draft has no number yet → 'Báo giá nháp'. */
export function quoteRefLabel(r: DeliverableQuoteRef): string {
  if (!r.number) return 'Báo giá nháp'
  return `${r.number}${r.version > 1 ? ` v${r.version}` : ''} · ${statusWord[r.status]}`
}

/** QA D-02: sent or accepted quotations — quoting the deliverable again would bill it twice. */
export const liveQuoteRefs = (refs: DeliverableQuoteRef[] | undefined): DeliverableQuoteRef[] =>
  (refs ?? []).filter(r => r.status !== 'DRAFT')
