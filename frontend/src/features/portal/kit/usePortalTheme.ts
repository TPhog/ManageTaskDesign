import { useLayoutEffect } from 'react'

/** Value written to `<html data-theme>` while a portal screen is mounted; `index.css` keys the Editorial tokens on it. */
export const PORTAL_THEME = 'editorial'

/**
 * Puts data-theme="editorial" on <html> while a portal screen is mounted, so Radix parts that render into <body>
 * (dialogs, sheets, toasts) pick up the editorial tokens too. Layout effect: no admin-coloured first frame.
 * Restores the previous value, so nested callers (layout + message) are safe.
 *
 * `PortalLayout`, `PortalMessage` and `PortalTokenPage` already call it — portal pages rendered inside them must not.
 * A new client-facing screen outside the layout (e.g. a public page) calls it once at its root.
 */
export function usePortalTheme(): void {
  useLayoutEffect(() => {
    const root = document.documentElement
    const previous = root.dataset.theme
    root.dataset.theme = PORTAL_THEME
    return () => {
      if (previous === undefined) delete root.dataset.theme
      else root.dataset.theme = previous
    }
  }, [])
}
