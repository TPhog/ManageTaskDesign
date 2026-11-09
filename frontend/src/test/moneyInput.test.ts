import { formatMoneyInput, parseMoneyInput } from '@/lib/moneyInput'

describe('parseMoneyInput', () => {
  it('keeps digits only', () => {
    expect(parseMoneyInput('2.000.000')).toBe(2_000_000)
    expect(parseMoneyInput('12a3')).toBe(123)
  })
  it('reads both "." and "," groups of three as thousands', () => {
    expect(parseMoneyInput('2,500,000')).toBe(2_500_000)
    expect(parseMoneyInput('2.500.000 ₫')).toBe(2_500_000)
    expect(parseMoneyInput('2500000')).toBe(2_500_000)
    expect(parseMoneyInput('2.500')).toBe(2500)
  })
  it('rejects decimals (null)', () => {
    expect(parseMoneyInput('1,5')).toBeNull()
    expect(parseMoneyInput('1.500.000,5')).toBeNull()
    expect(parseMoneyInput('2.000,00')).toBeNull()
  })
  it('treats empty as 0', () => expect(parseMoneyInput('')).toBe(0))
})

describe('formatMoneyInput', () => {
  it('groups thousands the Vietnamese way', () => expect(formatMoneyInput(2_000_000)).toBe('2.000.000'))
  it('shows 0', () => expect(formatMoneyInput(0)).toBe('0'))
})
