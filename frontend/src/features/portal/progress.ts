import type { PortalDeliverableSummary } from '@/lib/portalTypes'

/** Approved and delivered deliverables count as done. */
export function progressOf(s: PortalDeliverableSummary): { done: number; pct: number } {
  const done = s.delivered + s.approved
  return { done, pct: s.total === 0 ? 0 : Math.round((done * 100) / s.total) }
}

export function deliveredText(s: PortalDeliverableSummary): string {
  return s.total === 0 ? 'Chưa có hạng mục' : `${s.delivered}/${s.total} hạng mục đã bàn giao`
}

export function remainingRoundsText(d: { includedRounds: number; remainingRounds: number }): string {
  return d.includedRounds === 0
    ? 'Không gồm lần sửa miễn phí'
    : `Còn ${d.remainingRounds}/${d.includedRounds} lần sửa miễn phí`
}
