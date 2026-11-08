import { formatDate } from '@/lib/format'
import type { PortalRevisions } from '@/lib/portalTypes'

/** docs/08 §3.3: "Lần sửa" instead of "Round". */
export function roundTitle(r: { roundNo: number; receivedAt: string }): string {
  return `Lần sửa ${r.roundNo} · ${formatDate(r.receivedAt)}`
}

export function hasRounds(r: PortalRevisions): boolean {
  return r.projectRounds.length > 0 || r.deliverables.some(d => d.rounds.length > 0)
}
