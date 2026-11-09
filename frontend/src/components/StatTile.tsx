import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowUpRight } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface StatTileProps {
  to: string
  label: string
  value: ReactNode
  testId: string
  note?: ReactNode
  tone?: 'default' | 'hot'
  size?: 'lg' | 'md'
}

/** One metric; the whole tile links to the filtered list (docs/08 §2.2). `hot` = vermilion "needs you now". */
export function StatTile({ to, label, value, testId, note, tone = 'default', size = 'lg' }: StatTileProps) {
  return (
    <Link
      to={to}
      data-tone={tone}
      className={cn(
        'group flex h-full min-h-11 flex-col gap-2 rounded-lg border p-4 transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        tone === 'hot' ? 'tone-highlight border-transparent' : 'border-border bg-surface-sunk hover:border-border-strong',
      )}
    >
      <span className="flex items-start justify-between gap-2 text-sm font-medium text-muted-foreground">
        {label}
        <ArrowUpRight className="h-4 w-4 shrink-0" aria-hidden="true" />
      </span>
      <span
        data-testid={testId}
        className={cn('mt-auto break-words font-display font-bold tabular-nums text-foreground', size === 'lg' ? 'text-display-2xl' : 'text-display-md')}
      >
        {value}
      </span>
      {note && <span className="text-xs text-muted-foreground">{note}</span>}
    </Link>
  )
}
