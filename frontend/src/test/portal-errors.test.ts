import { isSessionExpired, logoutCompleted, sessionFailureKind } from '@/features/portal/errors'

describe('portal errors', () => {
  it('distinguishes rate limiting from an invalid link', () => {
    expect(sessionFailureKind({ status: 429 })).toBe('RATE_LIMITED')
    expect(sessionFailureKind({ status: 401 })).toBe('INVALID')
    expect(sessionFailureKind(new Error('network'))).toBe('INVALID')
  })

  it('treats only 401 as an expired session', () => {
    expect(isSessionExpired({ status: 401 })).toBe(true)
    expect(isSessionExpired({ status: 500 })).toBe(false)
    expect(isSessionExpired(new Error('network'))).toBe(false)
    expect(isSessionExpired(null)).toBe(false)
  })

  it('completes logout on success or 401 only', () => {
    expect(logoutCompleted(undefined)).toBe(true)
    expect(logoutCompleted({ status: 401 })).toBe(true)
    expect(logoutCompleted({ status: 403 })).toBe(false)
    expect(logoutCompleted(new Error('network'))).toBe(false)
  })
})
