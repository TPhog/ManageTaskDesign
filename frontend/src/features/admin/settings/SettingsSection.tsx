import type { ReactNode } from 'react'
import { Label } from '@/components/ui/label'
import { Tile, TileHeader } from '@/components/Tile'
import { cn } from '@/lib/utils'

/** A settings card: title + description on the left (top on mobile), the form on the right. */
export function SettingsSection({ id, title, description, children, className }: {
  id: string
  title: string
  description?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <Tile aria-labelledby={id} className={cn('p-5 sm:p-6', className)}>
      <div className="grid gap-x-10 gap-y-5 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <div className="min-w-0">
          <TileHeader id={id} title={title} />
          {description && <p className="mt-1.5 text-sm text-muted-foreground">{description}</p>}
        </div>
        <div className="min-w-0 space-y-5">{children}</div>
      </div>
    </Tile>
  )
}

/** The Save row at the bottom of a section form. */
export function SaveBar({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-center justify-end gap-3 border-t border-border pt-4">{children}</div>
}

export function Field({ id, label, hint, error, children }: {
  id: string
  label: ReactNode
  hint?: string
  error?: string
  children: ReactNode
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? (
        <p id={`${id}-error`} className="text-sm text-destructive">{error}</p>
      ) : hint ? (
        <p className="text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  )
}
