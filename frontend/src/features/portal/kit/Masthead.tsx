import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { splitWordmark } from './wordmark'

export interface MastheadProps {
  /** Studio display name; the last word is set in italic oxblood ("Studio *Mây*"). The link's accessible name is this text. */
  studioName: string
  /** Optional studio logo shown above the wordmark (decorative: `alt=""`, the name is already read). */
  logoUrl?: string | null
  /** Left side of the thin bar under the double rule, e.g. "Xin chào, Cà phê Mộc". Truncates on narrow screens. */
  subline: ReactNode
  /** Where the wordmark links to (the portal home). */
  homeHref: string
  /** Right side of the thin bar, e.g. the "Thoát" button. Keep every target ≥ 44px tall. */
  actions?: ReactNode
}

/**
 * Editorial masthead: centred serif wordmark, a 3px + 1px double rule, then a thin bar with the greeting and actions.
 * Only `PortalLayout` renders it; pages never add their own.
 */
export function Masthead({ studioName, logoUrl, subline, homeHref, actions }: MastheadProps) {
  const { head, tail } = splitWordmark(studioName)
  return (
    <header className="mx-auto max-w-2xl px-5 pt-5">
      <Link to={homeHref} className="flex min-h-11 flex-col items-center justify-center gap-2 text-center">
        {logoUrl && <img src={logoUrl} alt="" className="h-8 w-auto" />}
        <span className="font-display text-[1.625rem] font-medium leading-tight tracking-[-0.01em] text-foreground">
          {head}{tail && <i className="text-highlight-text">{tail}</i>}
        </span>
      </Link>
      <div className="mt-3.5 h-1.5 border-t-[3px] border-b border-rule" aria-hidden="true" />
      <div className="flex min-h-11 items-center justify-between gap-3 border-b border-rule text-xs">
        <p className="min-w-0 truncate text-muted-foreground">{subline}</p>
        {actions}
      </div>
    </header>
  )
}
