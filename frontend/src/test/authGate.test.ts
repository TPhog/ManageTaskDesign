import { describe, expect, it } from 'vitest'
import { authGate } from '@/lib/authGate'

const err = (status: number) => Object.assign(new Error('x'), { status })

describe('authGate', () => {
  it('is checking while the first /auth/me is pending', () => {
    expect(authGate({ status: 'pending', error: null })).toBe('checking')
  })

  it('is signed in only on success', () => {
    expect(authGate({ status: 'success', error: null })).toBe('signed-in')
  })

  it('treats 401 as signed out even if stale user data is still cached', () => {
    expect(authGate({ status: 'error', error: err(401) })).toBe('signed-out')
  })

  it('is checking while a cached error is being refetched', () => {
    expect(authGate({ status: 'error', fetchStatus: 'fetching', error: err(401) })).toBe('checking')
  })

  it('treats a 502 or network failure as unavailable, not signed out', () => {
    expect(authGate({ status: 'error', error: err(502) })).toBe('unavailable')
    expect(authGate({ status: 'error', error: new TypeError('Failed to fetch') })).toBe('unavailable')
  })
})
