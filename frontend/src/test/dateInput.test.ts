import { isoToVn, vnToIso, weekdayLabel } from '@/lib/dateInput'

describe('isoToVn', () => {
  it('shows an ISO date day-first', () => expect(isoToVn('2026-11-08')).toBe('08/11/2026'))
  it('is empty for empty or malformed values', () => {
    expect(isoToVn('')).toBe('')
    expect(isoToVn(null)).toBe('')
    expect(isoToVn('08/11/2026')).toBe('')
  })
})

describe('vnToIso', () => {
  it.each([
    ['08/11/2026', '2026-11-08'],
    ['8/11/2026', '2026-11-08'],
    ['8-11-2026', '2026-11-08'],
    ['08.11.2026', '2026-11-08'],
    ['08112026', '2026-11-08'],
    ['2026-11-08', '2026-11-08'],   // pasted ISO (and Playwright fill) still works
    [' 29/02/2028 ', '2028-02-29'],
  ])('reads %s', (text, iso) => expect(vnToIso(text)).toBe(iso))

  it.each(['31/02/2026', '29/02/2026', '00/11/2026', '12/13/2026', '1/1/26', 'abc', ''])('rejects %s', text =>
    expect(vnToIso(text)).toBeNull())
})

describe('weekdayLabel', () => {
  it('names the weekday so 11/08 is never misread', () => expect(weekdayLabel('2026-11-08')).toBe('Chủ Nhật, 08/11/2026'))
  it('is empty without a date', () => expect(weekdayLabel('')).toBe(''))
})
