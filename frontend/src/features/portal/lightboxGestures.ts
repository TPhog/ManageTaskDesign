/** Pure gesture maths for the portal lightbox (CF-06) — kept out of the component so it is unit-tested. */
export interface Point { x: number; y: number }

export const MIN_SCALE = 1
export const MAX_SCALE = 4
export const DOUBLE_TAP_SCALE = 2.5

export function clampScale(s: number): number {
  return Math.min(MAX_SCALE, Math.max(MIN_SCALE, s))
}

export function distance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y)
}

/** Two-finger zoom: the scale grows with the finger distance; a zero start distance keeps the scale. */
export function pinchScale(startScale: number, startDist: number, dist: number): number {
  if (startDist <= 0) return startScale
  return clampScale(startScale * (dist / startDist))
}

/** A horizontal move longer than `threshold` px pages the viewer; vertical or short moves do nothing. */
export function swipeDirection(dx: number, dy: number, threshold = 50): 'next' | 'prev' | null {
  if (Math.abs(dx) <= Math.abs(dy) || Math.abs(dx) < threshold) return null
  return dx < 0 ? 'next' : 'prev'
}

/** Second tap within `windowMs` and `slop` px of the first one. */
export function isDoubleTap(prev: { t: number; p: Point } | null, t: number, p: Point, windowMs = 300, slop = 24): boolean {
  if (!prev) return false
  return t - prev.t <= windowMs && distance(prev.p, p) <= slop
}
