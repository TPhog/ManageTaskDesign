import { formatDate, formatVnd } from '@/lib/format'

describe('formatVnd', () => {
  it('formats zero', () => {
    expect(formatVnd(0)).toMatch(/0/)
  })

  it('formats 2_000_000 as Vietnamese VND', () => {
    expect(formatVnd(2_000_000)).toContain('2.000.000')
  })
})

describe('formatDate', () => {
  it('returns em-dash for null', () => {
    expect(formatDate(null)).toBe('—')
  })

  it('formats ISO date string as dd/MM/yyyy', () => {
    expect(formatDate('2026-11-07T00:00:00Z')).toBe('07/11/2026')
  })
})
