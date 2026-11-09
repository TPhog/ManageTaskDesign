import { describeRecurrence, nextOccurrenceDate, recurrenceUntilError, toggleWeekday, WEEKDAY_BITS } from '@/features/admin/tasks/recurrenceUtils'

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

describe('nextOccurrenceDate (mirrors RecurrenceRule.NextAfter from max(due, today))', () => {
  const rule = (r: Partial<Parameters<typeof nextOccurrenceDate>[0]>) => ({ kind: 'DAILY' as const, weekdays: 0, monthDay: null, until: null, ...r })
  it('counts from the due date when it is ahead, else from today', () => {
    expect(nextOccurrenceDate(rule({}), '2026-11-20', '2026-11-09')).toBe('2026-11-21')
    expect(nextOccurrenceDate(rule({}), '2026-11-01', '2026-11-09')).toBe('2026-11-10')
    expect(nextOccurrenceDate(rule({}), null, '2026-11-09')).toBe('2026-11-10')
  })
  it('weekly picks the next ticked weekday strictly after the base', () => {
    // 2026-11-09 is a Monday; Mon|Wed
    expect(nextOccurrenceDate(rule({ kind: 'WEEKLY', weekdays: 1 | 4 }), '2026-11-09', '2026-11-09')).toBe('2026-11-11')
    expect(nextOccurrenceDate(rule({ kind: 'WEEKLY', weekdays: 1 }), '2026-11-09', '2026-11-09')).toBe('2026-11-16')
  })
  it('monthly clamps 29–31 to the end of a short month', () => {
    expect(nextOccurrenceDate(rule({ kind: 'MONTHLY', monthDay: 15 }), '2026-11-09', '2026-11-09')).toBe('2026-11-15')
    expect(nextOccurrenceDate(rule({ kind: 'MONTHLY', monthDay: 31 }), '2027-01-31', '2026-11-09')).toBe('2027-02-28')
    expect(nextOccurrenceDate(rule({ kind: 'MONTHLY', monthDay: 5 }), '2026-12-10', '2026-11-09')).toBe('2027-01-05')
  })
  it('is null past the end date', () => {
    expect(nextOccurrenceDate(rule({ until: '2026-11-09' }), '2026-11-09', '2026-11-09')).toBeNull()
  })
})

describe('recurrenceUntilError', () => {
  it('rejects an end date before the due date or today', () => {
    expect(recurrenceUntilError('2026-11-15', '2026-11-20', '2026-11-09')).toBe('Ngày kết thúc phải từ 20/11/2026 trở đi')
    expect(recurrenceUntilError('2026-11-05', null, '2026-11-09')).toBe('Ngày kết thúc phải từ 09/11/2026 trở đi')
    expect(recurrenceUntilError('2026-11-20', '2026-11-20', '2026-11-09')).toBeNull()
    expect(recurrenceUntilError(null, '2026-11-20', '2026-11-09')).toBeNull()
  })
})
