import type { ApiError } from './api'

export type AuthGate = 'checking' | 'signed-in' | 'signed-out' | 'unavailable'

/**
 * Decides what the admin shell should do from the `['me']` query.
 * React Query keeps the previous `data` when a refetch fails, so `data` alone must never mean
 * "signed in": a stale user next to an error used to bounce /admin ↔ /admin/login forever.
 * Only a 401 means signed out; any other failure (API down, 502, network) is "unavailable".
 */
export function authGate(q: { status: 'pending' | 'error' | 'success'; error: unknown }): AuthGate {
  if (q.status === 'pending') return 'checking'
  if (q.status === 'success') return 'signed-in'
  return (q.error as ApiError | null)?.status === 401 ? 'signed-out' : 'unavailable'
}
