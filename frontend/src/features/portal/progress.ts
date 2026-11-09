import type { PortalDeliverableSummary } from '@/lib/portalTypes'

/** Approved and delivered deliverables count as done. */
export function progressOf(s: PortalDeliverableSummary): { done: number; pct: number } {
  const done = s.delivered + s.approved
  return { done, pct: s.total === 0 ? 0 : Math.round((done * 100) / s.total) }
}

/** CF-09: "Đã xong 1/3 sản phẩm" — done = the designer confirmed the approval or delivered. */
export function deliveredText(s: PortalDeliverableSummary): string {
  return s.total === 0 ? 'Chưa có sản phẩm' : `Đã xong ${s.delivered + s.approved}/${s.total} sản phẩm`
}

/** Plan 12 glossary: "Còn 2 lượt chỉnh sửa miễn phí". */
export function remainingRoundsText(d: { includedRounds: number; remainingRounds: number }): string {
  if (d.includedRounds === 0) return 'Không gồm lượt chỉnh sửa miễn phí'
  if (d.remainingRounds <= 0) return 'Đã dùng hết lượt chỉnh sửa miễn phí'
  return `Còn ${d.remainingRounds} lượt chỉnh sửa miễn phí`
}

/** QA C-06 / Plan 12 BR-48: what still waits for the client, e.g. '1 sản phẩm chờ bạn duyệt'; '' when nothing does. */
export function waitingText(s: PortalDeliverableSummary): string {
  return s.awaitingClient > 0 ? `${s.awaitingClient} sản phẩm chờ bạn duyệt` : ''
}
