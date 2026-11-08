import { zaloUrl } from '@/features/portal/contact'
import { groupTimeline, timeOfDay, vnDay } from '@/features/portal/timeline'

const item = (occurredAt: string, message: string) => ({ occurredAt, message, kind: 'EVENT' as const })

describe('groupTimeline (QA C-12)', () => {
  const now = new Date('2026-11-08T05:00:00Z') // 12:00 in Vietnam

  it('groups newest-first items by Vietnamese calendar day', () => {
    const days = groupTimeline([
      item('2026-11-08T03:00:00Z', 'a'),   // 10:00 VN, today
      item('2026-11-07T20:00:00Z', 'b'),   // 03:00 VN on the 8th, today
      item('2026-11-07T10:00:00Z', 'c'),   // 17:00 VN on the 7th
      item('2026-11-01T10:00:00Z', 'd'),
    ], now)
    expect(days.map(d => d.label)).toEqual(['Hôm nay', 'Hôm qua', '01/11/2026'])
    expect(days.map(d => d.items.map(i => i.message))).toEqual([['a', 'b'], ['c'], ['d']])
  })

  it('is empty without items', () => expect(groupTimeline([], now)).toEqual([]))
})

describe('vnDay / timeOfDay', () => {
  it('uses Asia/Ho_Chi_Minh', () => {
    expect(vnDay('2026-11-07T20:00:00Z')).toBe('2026-11-08')
    expect(timeOfDay('2026-11-07T20:00:00Z')).toBe('03:00')
  })
})

describe('zaloUrl (QA C-04)', () => {
  it('links the phone number to Zalo', () => expect(zaloUrl('0901 234 567')).toBe('https://zalo.me/0901234567'))
})
