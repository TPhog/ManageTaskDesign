import type { LucideIcon } from 'lucide-react'

interface Props {
  icon: LucideIcon
  title: string
  description?: string
}

/** Full-screen message for the portal (invalid link, expired session, signed out). */
export function PortalMessage({ icon: Icon, title, description }: Props) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-6 text-center">
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-secondary text-primary">
        <Icon className="h-6 w-6" aria-hidden="true" />
      </div>
      <h1 className="text-lg font-semibold text-foreground">{title}</h1>
      {description && <p className="mt-2 max-w-xs text-sm text-muted-foreground">{description}</p>}
    </div>
  )
}
