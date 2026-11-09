import type { CalendarTask } from '@/lib/types'
import { addDays } from './taskUtils'

export type CalendarUnit = 'month' | 'week'

/** Weekday headers, Monday first (VN calendars start on Monday). */
export const WEEKDAY_HEADERS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']

const utc = (iso: string) => new Date(`${iso}T00:00:00Z`)

/** Monday of the week holding `iso` ('YYYY-MM-DD'); Sunday belongs to the previous Monday. */
export function weekStart(iso: string): string {
  const dow = utc(iso).getUTCDay()
  return addDays(iso, -((dow + 6) % 7))
}

/** Days shown by the calendar: six whole Monday-first weeks around the month, or one week. */
export function visibleRange(anchor: string, unit: CalendarUnit): { from: string; to: string; days: string[] } {
  const first = unit === 'month' ? weekStart(`${anchor.slice(0, 7)}-01`) : weekStart(anchor)
  const count = unit === 'month' ? 42 : 7
  const days = Array.from({ length: count }, (_, i) => addDays(first, i))
  return { from: days[0], to: days[days.length - 1], days }
}

/** Previous/next month (day clamped to the month's last day: 31/01 + 1 month = 28/02) or week. */
export function shiftAnchor(anchor: string, unit: CalendarUnit, dir: -1 | 1): string {
  if (unit === 'week') return addDays(anchor, 7 * dir)
  const d = utc(anchor)
  const target = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + dir, 1))
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
  target.setUTCDate(Math.min(d.getUTCDate(), lastDay))
  return target.toISOString().slice(0, 10)
}

/** Grid placement (1-based column) of a start→due bar clipped to `days`; null without both dates or outside. */
export function barSpan(task: Pick<CalendarTask, 'startDate' | 'dueDate'>, days: string[]): { col: number; span: number } | null {
  const { startDate, dueDate } = task
  if (!startDate || !dueDate || days.length === 0) return null
  const [lo, hi] = startDate <= dueDate ? [startDate, dueDate] : [dueDate, startDate]
  const first = lo < days[0] ? days[0] : lo
  const last = hi > days[days.length - 1] ? days[days.length - 1] : hi
  if (first > last) return null
  const col = days.indexOf(first)
  return { col: col + 1, span: days.indexOf(last) - col + 1 }
}

const dm = (iso: string) => `${iso.slice(8, 10)}/${iso.slice(5, 7)}`

/** "Tháng 11/2026" or "09/11 – 15/11/2026" (both years written when the week crosses New Year). */
export function rangeLabel(anchor: string, unit: CalendarUnit): string {
  if (unit === 'month') return `Tháng ${anchor.slice(5, 7)}/${anchor.slice(0, 4)}`
  const { from, to } = visibleRange(anchor, 'week')
  return from.slice(0, 4) === to.slice(0, 4)
    ? `${dm(from)} – ${dm(to)}/${to.slice(0, 4)}`
    : `${dm(from)}/${from.slice(0, 4)} – ${dm(to)}/${to.slice(0, 4)}`
}

/** The day a task chip sits on: its due date, else (start-only task) its start date. */
export const chipDay = (t: Pick<CalendarTask, 'startDate' | 'dueDate'>): string | null => t.dueDate ?? t.startDate

/**
 * The day a chip is listed on among `days` (month grid, phone week list): its chip day when visible, else — a
 * start→due span whose due date lies outside — the span's first visible day; null when nothing of it is visible.
 */
export function listDay(t: Pick<CalendarTask, 'startDate' | 'dueDate'>, days: string[]): string | null {
  const day = chipDay(t)
  if (day && days.includes(day)) return day
  const span = barSpan(t, days)
  return span ? days[span.col - 1] : null
}
