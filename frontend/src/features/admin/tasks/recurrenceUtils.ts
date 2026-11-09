import { formatDate } from '@/lib/format'
import type { Recurrence } from '@/lib/types'

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
