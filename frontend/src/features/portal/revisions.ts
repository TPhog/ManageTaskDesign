import { formatDate } from '@/lib/format'
import type { PortalRevisions } from '@/lib/portalTypes'

/** Plan 12 glossary: "Lượt chỉnh sửa" instead of "Round" / "Lần sửa". */
export function roundTitle(r: { roundNo: number; receivedAt: string }): string {
  return `Lượt chỉnh sửa ${r.roundNo} · ${formatDate(r.receivedAt)}`
}

export function hasRounds(r: PortalRevisions): boolean {
  return r.projectRounds.length > 0 || r.deliverables.some(d => d.rounds.length > 0)
}
