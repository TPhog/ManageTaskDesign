import { Fragment } from 'react'
import { safeHref } from '@/lib/markdown'
import { cn } from '@/lib/utils'

const URL_PATTERN = /(https?:\/\/[^\s<>"']+)/g
const TRAILING_PUNCTUATION = /[.,;:!?)]+$/

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
        const trailing = TRAILING_PUNCTUATION.exec(part)?.[0] ?? ''
        const url = trailing ? part.slice(0, -trailing.length) : part
        const href = safeHref(url)
        if (!href) return <Fragment key={i}>{part}</Fragment>
        return (
          <Fragment key={i}>
            <a
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              referrerPolicy="no-referrer"
              className="font-medium text-primary underline underline-offset-2 hover:text-primary/80"
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
