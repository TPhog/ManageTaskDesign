import type { HTMLAttributes, ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

export type TileTone = 'default' | 'inverse' | 'highlight'

const toneClass: Record<TileTone, string> = {
  default: 'border-border bg-card text-card-foreground',
  inverse: 'tone-inverse border-transparent',
  highlight: 'tone-highlight border-transparent',
}

export interface TileProps extends HTMLAttributes<HTMLElement> {
  as?: 'section' | 'div' | 'article'
  tone?: TileTone
}

/** One bento tile: 20px radius, 1px warm border, faint shadow. `inverse` = solid ink, `highlight` = vermilion with ink text. */
export function Tile({ as: Comp = 'section', tone = 'default', className, ...rest }: TileProps) {
  return <Comp data-tone={tone} className={cn('min-w-0 rounded-xl border p-5 shadow-card', toneClass[tone], className)} {...rest} />
}

export function TileHeader({ title, icon: Icon, meta, action, id, level = 2 }: {
  title: string
  icon?: LucideIcon
  meta?: ReactNode
  action?: ReactNode
  id?: string
  level?: 2 | 3
}) {
  const Heading = level === 2 ? 'h2' : 'h3'
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
      <Heading id={id} className="flex min-w-0 items-center gap-2 font-display text-title font-bold text-foreground">
        {Icon && <Icon className="h-[18px] w-[18px] shrink-0" aria-hidden="true" />}
        {title}
      </Heading>
      {(meta || action) && <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">{meta}{action}</div>}
    </div>
  )
}
