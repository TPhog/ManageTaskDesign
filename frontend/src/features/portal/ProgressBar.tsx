import type { PortalDeliverableSummary } from '@/lib/portalTypes'
import { progressOf } from './progress'

/** Two segments on a hairline track: delivered (ink) then approved (moss) — the legend under it uses the same marks. */
export function ProgressBar({ summary }: { summary: PortalDeliverableSummary }) {
  const { pct } = progressOf(summary)
  const width = (n: number) => `${summary.total === 0 ? 0 : (n * 100) / summary.total}%`

  return (
    <div
      role="progressbar"
      aria-label="Tiến độ sản phẩm"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      className="flex h-1 w-full overflow-hidden bg-border"
    >
      <div className="h-full bg-foreground transition-all motion-reduce:transition-none" style={{ width: width(summary.delivered) }} />
      <div className="h-full bg-success transition-all motion-reduce:transition-none" style={{ width: width(summary.approved) }} />
    </div>
  )
}
