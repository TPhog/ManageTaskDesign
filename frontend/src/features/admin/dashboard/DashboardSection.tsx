import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { Tile, TileHeader, type TileTone } from '@/components/Tile'
import { cn } from '@/lib/utils'

/** House style for a dashboard block: a bento Tile named for screen readers, with an icon + h2 title. */
export function Section({ title, icon, children, className, tone, meta, action }: {
  title: string
  icon: LucideIcon
  children: ReactNode
  className?: string
  tone?: TileTone
  meta?: ReactNode
  action?: ReactNode
}) {
  return (
    <Tile aria-label={title} tone={tone} className={cn('space-y-4', className)}>
      <TileHeader title={title} icon={icon} meta={meta} action={action} />
      {children}
    </Tile>
  )
}
