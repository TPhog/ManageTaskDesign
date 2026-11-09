import { describeRecurrence, toggleWeekday, WEEKDAY_BITS } from '@/features/admin/tasks/recurrenceUtils'

describe('recurrenceUtils', () => {
  it('toggleWeekday flips one bit', () => {
    expect(toggleWeekday(0, 1)).toBe(1)
    expect(toggleWeekday(5, 4)).toBe(1)
    expect(WEEKDAY_BITS.map(w => w.bit)).toEqual([1, 2, 4, 8, 16, 32, 64])
  })
  it('describeRecurrence is plain Vietnamese', () => {
    expect(describeRecurrence(null)).toBe('Không lặp')
    expect(describeRecurrence({ kind: 'DAILY', weekdays: 0, monthDay: null, until: null })).toBe('Hằng ngày')
    expect(describeRecurrence({ kind: 'WEEKLY', weekdays: 1 | 4, monthDay: null, until: null })).toBe('Hằng tuần: T2, T4')
    expect(describeRecurrence({ kind: 'MONTHLY', weekdays: 0, monthDay: 15, until: '2027-01-31' })).toBe('Hằng tháng, ngày 15 · đến 31/01/2027')
  })
})
