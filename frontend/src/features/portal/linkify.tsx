import type { ReactNode } from 'react'

const URL_RE = /https?:\/\/[^\s<>"']+/g
const TRAILING = /[.,;:!?)\]}»”’]+$/

/**
 * Plain text with http(s) URLs turned into safe links (delivery messages carry the hand-over link).
 * Only http/https ever becomes a link; everything else stays text, so `javascript:` and friends never render as links.
 */
export function linkify(text: string): ReactNode[] {
  const out: ReactNode[] = []
  let last = 0
  for (const m of text.matchAll(URL_RE)) {
    const start = m.index ?? 0
    const raw = m[0]
    const url = raw.replace(TRAILING, '')
    let safe = false
    try {
      const parsed = new URL(url)
      safe = parsed.protocol === 'http:' || parsed.protocol === 'https:'
    } catch {
      safe = false
    }
    if (!safe) continue
    if (start > last) out.push(text.slice(last, start))
    out.push(
      <a key={`${start}-${url}`} href={url} target="_blank" rel="noopener noreferrer" className="link-inline break-all">
        {url}
      </a>,
    )
    last = start + url.length
  }
  if (last < text.length) out.push(text.slice(last))
  return out
}
