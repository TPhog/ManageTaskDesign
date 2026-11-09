import { quotaTone, type QuotaTone } from '@/lib/revisionLabels'

interface QuotaMeterProps {
  used: number
  total: number
  label?: string
}

const barColor: Record<QuotaTone, string> = { ok: 'bg-foreground', warn: 'bg-warning', over: 'bg-danger' }
const textColor: Record<QuotaTone, string> = {
  ok: 'text-muted-foreground',
  warn: 'text-warning font-medium',
  over: 'text-danger font-medium',
}

/** docs/08 §1: "2/3 vòng" + bar; warning with one free round left, danger when none is left */
export function QuotaMeter({ used, total, label }: QuotaMeterProps) {
  // no free rounds included (e.g. a service without revisions): a full red bar would read as an error
  if (total === 0) {
    return (
      <span className="text-xs text-muted-foreground">
        Không có vòng sửa miễn phí{used > 0 ? ` · đã sửa ${used} vòng` : ''}
      </span>
    )
  }
  const pct = total === 0 ? 100 : Math.min(100, Math.round((used / total) * 100))
  const tone = quotaTone(used, total)

  return (
    <div className="flex items-center gap-2 min-w-0">
      <div className="flex-1 h-1.5 bg-border rounded-full overflow-hidden" aria-hidden="true">
        <div className={`h-full rounded-full transition-all ${barColor[tone]}`} style={{ width: `${pct}%` }} />
      </div>
      <span className={`text-xs tabular-nums whitespace-nowrap ${textColor[tone]}`}>
        {used}/{total}{label ? ` ${label}` : ''}
      </span>
    </div>
  )
}
