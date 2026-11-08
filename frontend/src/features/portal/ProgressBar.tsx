import type { PortalDeliverableSummary } from '@/lib/portalTypes'
import { progressOf } from './progress'

/** Two-segment bar: delivered (emerald) then approved by the client (sky) — distinct hues, QA C-07. */
export function ProgressBar({ summary }: { summary: PortalDeliverableSummary }) {
  const { pct } = progressOf(summary)
  const width = (n: number) => `${summary.total === 0 ? 0 : (n * 100) / summary.total}%`

  return (
    <div
      role="progressbar"
      aria-label="Tiến độ hạng mục"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      className="flex h-2 w-full overflow-hidden rounded-full bg-slate-100"
    >
      <div className="h-full bg-emerald-600 transition-all" style={{ width: width(summary.delivered) }} />
      <div className="h-full bg-sky-400 transition-all" style={{ width: width(summary.approved) }} />
    </div>
  )
}
