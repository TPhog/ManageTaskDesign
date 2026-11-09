import {
  canSearch, clientHref, projectHref, quotationHref, quotationLabel, requestSearchHref, resultCount, searchGroups, searchTerm,
  shortcutLabel, taskSearchHref,
} from '@/features/admin/search/searchUtils'

describe('searchTerm / canSearch', () => {
  it('trims and collapses whitespace like the API', () => {
    expect(searchTerm('  Nguyễn   Văn ')).toBe('Nguyễn Văn')
  })

  it('needs at least two characters', () => {
    expect(canSearch(searchTerm(' a '))).toBe(false)
    expect(canSearch('ab')).toBe(true)
  })
})

describe('result helpers', () => {
  it('counts every group', () => {
    expect(resultCount(undefined)).toBe(0)
    expect(resultCount({
      clients: [{ id: 'c', name: 'A', company: null, phone: null, status: 'ACTIVE' }],
      projects: [],
      quotations: [{ id: 'q', projectId: 'p', number: null, version: 1, title: 'T', projectName: 'P', clientName: 'A', effectiveStatus: 'DRAFT', total: 0 }],
    })).toBe(2)
  })

  it('links each kind to its page; clients open the filtered client list', () => {
    expect(clientHref({ id: 'abc-1' })).toBe('/admin/clients/abc-1')
    expect(projectHref({ id: 'p1' })).toBe('/admin/projects/p1')
    expect(quotationHref({ id: 'q1' })).toBe('/admin/quotations/q1')
  })

  it('labels drafts without a number', () => {
    expect(quotationLabel({ number: 'BG-2026-0007', version: 2 })).toBe('BG-2026-0007 · v2')
    expect(quotationLabel({ number: null, version: 1 })).toBe('Báo giá nháp')
  })

  it('shows the platform shortcut', () => {
    expect(shortcutLabel('MacIntel')).toBe('⌘K')
    expect(shortcutLabel('Win32')).toBe('Ctrl K')
  })
})

describe('tasks and requests groups', () => {
  const base = { clients: [], projects: [], quotations: [] }

  it('links a task to its project tab and a request to its page', () => {
    expect(taskSearchHref({ id: 't1', projectId: 'p1' })).toBe('/admin/projects/p1?tab=tasks&task=t1')
    expect(requestSearchHref({ id: 'r1' })).toBe('/admin/requests/r1')
  })

  it('keeps the group order and labels', () => {
    const result = {
      ...base,
      clients: [{ id: 'c', name: 'A', company: null, phone: null, status: 'ACTIVE' as const }],
      tasks: [{ id: 't', projectId: 'p', title: 'T', projectName: 'P', status: 'TODO' as const }],
      requests: [{ id: 'r', projectId: 'p', projectName: 'P', clientName: 'A', excerpt: '…đổi màu…', status: 'NEW' as const }],
    }
    expect(searchGroups(result).map(g => g.label)).toEqual(['Khách hàng', 'Công việc', 'Yêu cầu'])
    expect(resultCount(result)).toBe(3)
  })

  it('an older server without tasks / requests still works', () => {
    expect(searchGroups(base)).toEqual([])
    expect(resultCount(base)).toBe(0)
  })
})
