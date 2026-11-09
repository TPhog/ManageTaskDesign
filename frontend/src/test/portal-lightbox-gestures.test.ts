import { clampScale, isDoubleTap, pinchScale, swipeDirection } from '@/features/portal/lightboxGestures'

describe('lightbox gestures (CF-06)', () => {
  it('clamps zoom between 1x and 4x', () => {
    expect(clampScale(0.5)).toBe(1)
    expect(clampScale(9)).toBe(4)
    expect(pinchScale(1, 100, 250)).toBe(2.5)
    expect(pinchScale(2, 0, 50)).toBe(2)
  })

  it('reads a horizontal swipe and ignores vertical or short moves', () => {
    expect(swipeDirection(-80, 10)).toBe('next')
    expect(swipeDirection(80, -5)).toBe('prev')
    expect(swipeDirection(30, 0)).toBeNull()
    expect(swipeDirection(-80, 120)).toBeNull()
  })

  it('detects a double tap close in time and place', () => {
    const first = { t: 1000, p: { x: 100, y: 100 } }
    expect(isDoubleTap(first, 1250, { x: 110, y: 104 })).toBe(true)
    expect(isDoubleTap(first, 1400, { x: 100, y: 100 })).toBe(false)
    expect(isDoubleTap(first, 1200, { x: 200, y: 100 })).toBe(false)
    expect(isDoubleTap(null, 1200, { x: 100, y: 100 })).toBe(false)
  })
})
