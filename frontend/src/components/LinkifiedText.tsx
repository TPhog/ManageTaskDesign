import { Fragment } from 'react'
import { safeHref } from '@/lib/markdown'
import { cn } from '@/lib/utils'

const URL_PATTERN = /(https?:\/\/[^\s<>"']+)/g

/** Peels trailing punctuation off a URL; a ")" stays when the URL has a matching "(" (wikipedia style). */
function splitTrailing(part: string): [string, string] {
  let end = part.length
  while (end > 0 && /[.,;:!?)]/.test(part[end - 1])) {
    if (part[end - 1] === ')') {
      const head = part.slice(0, end)
      if (head.split('(').length >= head.split(')').length) break
    }
    end--
  }
  return [part.slice(0, end), part.slice(end)]
}

/**
 * Plain text where http(s) URLs become safe links (the delivery link on the portal timeline).
 * Text stays text: no HTML is interpreted, and every URL goes through safeHref.
 */
export function LinkifiedText({ text, className }: { text: string; className?: string }) {
  return (
    <span className={cn('whitespace-pre-wrap break-words', className)}>
      {text.split(URL_PATTERN).map((part, i) => {
        // odd indexes are the captured URLs
        if (i % 2 === 0) return <Fragment key={i}>{part}</Fragment>
        const [url, trailing] = splitTrailing(part)
        const href = safeHref(url)
        if (!href) return <Fragment key={i}>{part}</Fragment>
        return (
          <Fragment key={i}>
            <a
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              referrerPolicy="no-referrer"
              className="link-inline break-words"
            >
              {url}
            </a>
            {trailing}
          </Fragment>
        )
      })}
    </span>
  )
}
