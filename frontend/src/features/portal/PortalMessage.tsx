import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { usePortalTheme } from './kit/usePortalTheme'

interface Props {
  icon: LucideIcon
  title: string
  description?: string
  action?: ReactNode
}

/** Full-screen message for the portal (invalid link, expired session, signed out). */
export function PortalMessage({ icon: Icon, title, description, action }: Props) {
  usePortalTheme()
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-background px-6 text-center">
      <div className="mb-4 flex h-12 w-12 items-center justify-center border border-rule text-highlight-text">
        <Icon className="h-6 w-6" aria-hidden="true" />
      </div>
      <h1 className="font-display text-2xl font-medium leading-tight text-foreground">{title}</h1>
      {description && <p className="mt-2 max-w-xs text-sm text-muted-foreground">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}
