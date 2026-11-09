import { ArrowUpRight } from 'lucide-react'
import { Kicker } from './kit/editorial'

/** P15 D6: links the designer shared for one deliverable (Figma, Drive…). Only http(s) URLs become anchors. */
export function PortalDeliverableLinks({ links }: { links: { title: string; url: string }[] }) {
  const safe = links.filter(l => /^https?:\/\//i.test(l.url))
  if (safe.length === 0) return null
  return (
    <div className="mt-3">
      <Kicker>Tài liệu cho bạn</Kicker>
      <ul className="m-0 mt-1 list-none p-0">
        {safe.map((l, i) => (
          <li key={`${l.url}-${i}`}>
            <a
              href={l.url}
              target="_blank"
              rel="noopener noreferrer"
              className="link-inline inline-flex min-h-11 max-w-full items-center gap-1.5 py-1 text-sm"
            >
              <span className="min-w-0 break-words [overflow-wrap:anywhere]">{l.title}</span>
              <ArrowUpRight className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span className="sr-only"> (mở trong thẻ mới)</span>
            </a>
          </li>
        ))}
      </ul>
    </div>
  )
}
