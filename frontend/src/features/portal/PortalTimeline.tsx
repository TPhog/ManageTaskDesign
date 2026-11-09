import { useState } from 'react'
import { Button } from '@/components/ui/button'
import type { PortalTimelineItem } from '@/lib/portalTypes'
import { Kicker } from './kit/editorial'
import { linkify } from './linkify'
import { PortalImageStrip } from './PortalImagesSection'
import { groupTimeline, timeOfDay } from './timeline'

interface Props {
  items: PortalTimelineItem[]
  /** how many of the newest items show before "Xem thêm" (CF-15) */
  initial?: number
}

/** "Hoạt động gần đây": newest first, grouped by Vietnamese day, collapsed to the five newest (CF-15). */
export function PortalTimeline({ items, initial = 5 }: Props) {
  const [all, setAll] = useState(false)
  const shown = all ? items : items.slice(0, initial)

  return (
    <div className="mt-3 space-y-5">
      {groupTimeline(shown).map(day => (
        <div key={day.day}>
          <h3><Kicker>{day.label}</Kicker></h3>
          <ol className="m-0 mt-1 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
            {day.items.map((t, i) => (
              <li key={`${t.occurredAt}-${i}`} className="grid grid-cols-[3rem_minmax(0,1fr)] gap-2 py-3">
                <time dateTime={t.occurredAt} className="pt-0.5 font-serif text-sm italic tabular-nums text-muted-foreground">
                  {timeOfDay(t.occurredAt)}
                </time>
                <div className="min-w-0">
                  {t.kind === 'MESSAGE' ? (
                    <>
                      <Kicker tone="highlight">Nhà thiết kế nhắn</Kicker>
                      <blockquote className="m-0 mt-1 whitespace-pre-wrap break-words border-l-2 border-highlight pl-3 font-serif text-base italic leading-relaxed text-foreground">
                        {linkify(t.message)}
                      </blockquote>
                    </>
                  ) : (
                    <p className="whitespace-pre-wrap break-words font-serif text-base leading-snug text-foreground">{linkify(t.message)}</p>
                  )}
                  <PortalImageStrip images={t.images ?? []} />
                </div>
              </li>
            ))}
          </ol>
        </div>
      ))}
      {items.length > initial && !all && (
        <Button type="button" variant="outline" className="h-11 w-full" onClick={() => setAll(true)}>
          {`Xem thêm ${items.length - initial} hoạt động`}
        </Button>
      )}
    </div>
  )
}
