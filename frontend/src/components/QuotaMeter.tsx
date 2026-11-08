import { quotaTone, type QuotaTone } from '@/lib/revisionLabels'

interface QuotaMeterProps {
  used: number
  total: number
  label?: string
}

const barColor: Record<QuotaTone, string> = { ok: 'bg-primary', warn: 'bg-amber-500', over: 'bg-red-500' }
const textColor: Record<QuotaTone, string> = {
  ok: 'text-muted-foreground',
  warn: 'text-amber-700 font-medium',
  over: 'text-red-700 font-medium',
}

/** docs/08 §1: "2/3 vòng" + bar; amber with one free round left, red when none is left */
export function QuotaMeter({ used, total, label }: QuotaMeterProps) {
  const pct = total === 0 ? 100 : Math.min(100, Math.round((used / total) * 100))
  const tone = quotaTone(used, total)

  return (
    <div className="flex items-center gap-2 min-w-0">
      <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden" aria-hidden="true">
        <div className={`h-full rounded-full transition-all ${barColor[tone]}`} style={{ width: `${pct}%` }} />
      </div>
      <span className={`text-xs tabular-nums whitespace-nowrap ${textColor[tone]}`}>
        {used}/{total}{label ? ` ${label}` : ''}
      </span>
    </div>
  )
}
