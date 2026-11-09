import { barSpan, rangeLabel, shiftAnchor, visibleRange, weekStart } from '@/features/admin/tasks/calendarUtils'

describe('weekStart', () => {
  it('returns the Monday of the week (Sunday belongs to the previous Monday)', () => {
    expect(weekStart('2026-11-08')).toBe('2026-11-02') // Sunday
    expect(weekStart('2026-11-09')).toBe('2026-11-09') // Monday
    expect(weekStart('2026-11-11')).toBe('2026-11-09')
  })
})

describe('visibleRange', () => {
  it('month view covers whole Monday-first weeks around the month', () => {
    const r = visibleRange('2026-11-15', 'month')
    expect(r.from).toBe('2026-10-26')
    expect(r.to).toBe('2026-12-06')
    expect(r.days).toHaveLength(42)
    expect(r.days.length).toBeLessThanOrEqual(62)
  })
  it('week view is exactly seven days from Monday', () => {
    const r = visibleRange('2026-11-11', 'week')
    expect(r.days).toEqual(['2026-11-09', '2026-11-10', '2026-11-11', '2026-11-12', '2026-11-13', '2026-11-14', '2026-11-15'])
  })
})

describe('shiftAnchor', () => {
  it('moves by month or week', () => {
    expect(shiftAnchor('2026-11-15', 'month', 1)).toBe('2026-12-15')
    expect(shiftAnchor('2026-01-31', 'month', 1)).toBe('2026-02-28')
    expect(shiftAnchor('2026-11-15', 'week', -1)).toBe('2026-11-08')
  })
})

describe('barSpan', () => {
  const days = ['2026-11-09', '2026-11-10', '2026-11-11', '2026-11-12', '2026-11-13', '2026-11-14', '2026-11-15']
  it('clips a span to the visible week (1-based grid column)', () => {
    expect(barSpan({ startDate: '2026-11-07', dueDate: '2026-11-11' }, days)).toEqual({ col: 1, span: 3 })
    expect(barSpan({ startDate: '2026-11-12', dueDate: '2026-11-20' }, days)).toEqual({ col: 4, span: 4 })
  })
  it('is null without both dates or when outside the week', () => {
    expect(barSpan({ startDate: null, dueDate: '2026-11-11' }, days)).toBeNull()
    expect(barSpan({ startDate: '2026-11-20', dueDate: '2026-11-22' }, days)).toBeNull()
  })
})

describe('rangeLabel', () => {
  it('formats in Vietnamese', () => {
    expect(rangeLabel('2026-11-15', 'month')).toBe('Tháng 11/2026')
    expect(rangeLabel('2026-11-11', 'week')).toBe('09/11 – 15/11/2026')
  })
})
