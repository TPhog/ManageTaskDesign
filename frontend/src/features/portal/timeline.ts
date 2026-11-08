import type { PortalTimelineItem } from '@/lib/portalTypes'

export interface TimelineDay {
  /** 'YYYY-MM-DD' in Vietnam */
  day: string
  label: string
  items: PortalTimelineItem[]
}

/** Calendar day of an instant in Asia/Ho_Chi_Minh, 'YYYY-MM-DD' (BR-06). */
export function vnDay(instant: string | Date): string {
  return new Date(instant).toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' })
}

/** '03:00' — the time of an instant in Vietnam. */
export function timeOfDay(instant: string | Date): string {
  return new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'Asia/Ho_Chi_Minh' })
    .format(new Date(instant))
}

/** QA C-12: newest-first items grouped by Vietnamese day — "Hôm nay", "Hôm qua", then dd/mm/yyyy. */
export function groupTimeline(items: PortalTimelineItem[], now: Date = new Date()): TimelineDay[] {
  const today = vnDay(now)
  const yesterday = vnDay(new Date(now.getTime() - 86_400_000))
  const days: TimelineDay[] = []
  for (const item of items) {
    const day = vnDay(item.occurredAt)
    let group = days[days.length - 1]
    if (!group || group.day !== day) {
      const [y, m, d] = day.split('-')
      group = { day, label: day === today ? 'Hôm nay' : day === yesterday ? 'Hôm qua' : `${d}/${m}/${y}`, items: [] }
      days.push(group)
    }
    group.items.push(item)
  }
  return days
}
