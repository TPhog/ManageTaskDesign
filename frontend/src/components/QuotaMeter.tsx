interface QuotaMeterProps {
  used: number
  total: number
  label?: string
}

export function QuotaMeter({ used, total, label }: QuotaMeterProps) {
  const pct = total === 0 ? 100 : Math.min(100, Math.round((used / total) * 100))
  const isOver = used > total
  const color = isOver ? 'bg-red-500' : pct >= 80 ? 'bg-orange-400' : 'bg-blue-500'

  return (
    <div className="flex items-center gap-2 min-w-0">
      <div className="flex-1 h-1.5 bg-gray-200 rounded-full overflow-hidden">
        <div className={`h-full rounded-full transition-all ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className={`text-xs tabular-nums whitespace-nowrap ${isOver ? 'text-red-600 font-medium' : 'text-gray-500'}`}>
        {used}/{total}{label ? ` ${label}` : ''}
      </span>
    </div>
  )
}
