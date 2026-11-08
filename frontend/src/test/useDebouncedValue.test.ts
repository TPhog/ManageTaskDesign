import { act, renderHook } from '@testing-library/react'
import { useDebouncedValue } from '@/lib/useDebouncedValue'

describe('useDebouncedValue', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('returns the latest value only once it stopped changing for the delay', () => {
    const { result, rerender } = renderHook(({ v }) => useDebouncedValue(v, 250), { initialProps: { v: 'a' } })
    expect(result.current).toBe('a')

    rerender({ v: 'ab' })
    act(() => { vi.advanceTimersByTime(200) })
    expect(result.current).toBe('a')

    rerender({ v: 'abc' })
    act(() => { vi.advanceTimersByTime(200) })
    expect(result.current).toBe('a')

    act(() => { vi.advanceTimersByTime(50) })
    expect(result.current).toBe('abc')
  })
})
