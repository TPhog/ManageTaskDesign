import { DEFAULT_VIEW_CONFIG, configToSearch, sameConfig } from '@/features/admin/tasks/taskUtils'

describe('saved view config helpers', () => {
  it('sameConfig ignores display labels but compares filters and grouping', () => {
    const a = { ...DEFAULT_VIEW_CONFIG, groupBy: 'due' as const, clientId: 'c1', clientLabel: 'Khách A' }
    expect(sameConfig(a, { ...a, clientLabel: 'Khách A (đổi tên)' })).toBe(true)
    expect(sameConfig(a, { ...a, groupBy: 'priority' })).toBe(false)
    expect(sameConfig(a, { ...a, mode: 'calendar' })).toBe(false)
  })

  it('configToSearch writes only non-default keys', () => {
    expect(configToSearch(DEFAULT_VIEW_CONFIG).toString()).toBe('')
    const s = configToSearch({ ...DEFAULT_VIEW_CONFIG, mode: 'calendar', due: 'week', status: 'REVIEW', groupBy: 'project' })
    expect(s.get('view')).toBe('calendar')
    expect(s.get('due')).toBe('week')
    expect(s.get('status')).toBe('REVIEW')
    expect(s.get('groupBy')).toBe('project')
  })
})
