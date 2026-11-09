import { formatDate } from '@/lib/format'
import type { Recurrence } from '@/lib/types'
import { addDays } from './taskUtils'

/** Weekday bitmask used by the API: Mon=1 … Sun=64. */
export const WEEKDAY_BITS: { bit: number; short: string; full: string }[] = [
  { bit: 1, short: 'T2', full: 'Thứ hai' },
  { bit: 2, short: 'T3', full: 'Thứ ba' },
  { bit: 4, short: 'T4', full: 'Thứ tư' },
  { bit: 8, short: 'T5', full: 'Thứ năm' },
  { bit: 16, short: 'T6', full: 'Thứ sáu' },
  { bit: 32, short: 'T7', full: 'Thứ bảy' },
  { bit: 64, short: 'CN', full: 'Chủ nhật' },
]

export function toggleWeekday(mask: number, bit: number): number {
  return mask ^ bit
}

/** "Hằng ngày" | "Hằng tuần: T2, T4" | "Hằng tháng, ngày 15", plus " · đến dd/MM/yyyy" when it ends. */
export function describeRecurrence(r: Recurrence | null): string {
  if (!r) return 'Không lặp'
  const base =
    r.kind === 'DAILY'
      ? 'Hằng ngày'
      : r.kind === 'WEEKLY'
        ? `Hằng tuần: ${WEEKDAY_BITS.filter(w => r.weekdays & w.bit).map(w => w.short).join(', ')}`
        : `Hằng tháng, ngày ${r.monthDay}`
  return r.until ? `${base} · đến ${formatDate(r.until)}` : base
}

const utc = (iso: string) => new Date(`${iso}T00:00:00Z`)
const daysInMonth = (year: number, month0: number) => new Date(Date.UTC(year, month0 + 1, 0)).getUTCDate()
const ymd = (year: number, month0: number, day: number) => new Date(Date.UTC(year, month0, day)).toISOString().slice(0, 10)

/** The date the next occurrence would count from if the task were marked DONE today: max(due, today), like the server. */
export const recurrenceBase = (dueDate: string | null, today: string): string => (dueDate && dueDate > today ? dueDate : today)

/** Due date of the copy created on DONE today (server RecurrenceRule.NextAfter); null when it falls past the end date. */
export function nextOccurrenceDate(r: Recurrence, dueDate: string | null, today: string): string | null {
  const base = recurrenceBase(dueDate, today)
  let next: string | null = null
  if (r.kind === 'DAILY') next = addDays(base, 1)
  else if (r.kind === 'WEEKLY') {
    for (let i = 1; i <= 7 && !next; i++) {
      const day = addDays(base, i)
      if (r.weekdays & (1 << ((utc(day).getUTCDay() + 6) % 7))) next = day
    }
  } else if (r.monthDay) {
    const d = utc(base)
    const inMonth = (y: number, m0: number) => ymd(y, m0, Math.min(r.monthDay!, daysInMonth(y, m0)))
    const candidate = inMonth(d.getUTCFullYear(), d.getUTCMonth())
    const following = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1))
    next = candidate > base ? candidate : inMonth(following.getUTCFullYear(), following.getUTCMonth())
  }
  return next && r.until && next > r.until ? null : next
}

/** An end date before the due date (or today) would stop the rule before it ever repeats. */
export function recurrenceUntilError(until: string | null, dueDate: string | null, today: string): string | null {
  const base = recurrenceBase(dueDate, today)
  return until && until < base ? `Ngày kết thúc phải từ ${formatDate(base)} trở đi` : null
}
