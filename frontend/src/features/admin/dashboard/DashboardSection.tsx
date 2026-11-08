import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'

export const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

/** House style for a dashboard block: card with an icon + h2 title; the section is named for screen readers. */
export function Section({ title, icon: Icon, children }: { title: string; icon: LucideIcon; children: ReactNode }) {
  return (
    <section aria-label={title} className={`${panelClass} min-w-0 space-y-4`}>
      <h2 className="flex items-center gap-2 text-base font-semibold text-foreground">
        <Icon className="h-[18px] w-[18px] text-primary" aria-hidden="true" />
        {title}
      </h2>
      {children}
    </section>
  )
}
