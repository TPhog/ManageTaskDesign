export type PortalSessionFailure = 'INVALID' | 'RATE_LIMITED'

/** Maps a failed POST /portal/session to the message the client should see. */
export function sessionFailureKind(err: unknown): PortalSessionFailure {
  return (err as { status?: number } | null)?.status === 429 ? 'RATE_LIMITED' : 'INVALID'
}

/** Only a 401 from /portal/me means the session is gone; anything else may be transient. */
export function isSessionExpired(err: unknown): boolean {
  return (err as { status?: number } | null)?.status === 401
}

/** Logout is "done" when it succeeded or the session was already gone (401). */
export function logoutCompleted(err: unknown): boolean {
  return err == null || isSessionExpired(err)
}
