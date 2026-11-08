import {
  canSearch, clientHref, projectHref, quotationHref, quotationLabel, resultCount, searchTerm, shortcutLabel,
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
    expect(clientHref({ name: 'Cà Phê & Co' })).toBe('/admin/clients?q=C%C3%A0%20Ph%C3%AA%20%26%20Co')
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
