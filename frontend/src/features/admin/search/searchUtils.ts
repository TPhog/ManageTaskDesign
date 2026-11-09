import type { SearchQuotation, SearchRequest, SearchResult, SearchTask } from '@/lib/searchTypes'

/** Same bound as the API (SearchQuery.MinLength): shorter terms are not sent. */
export const SEARCH_MIN_LENGTH = 2

export function searchTerm(text: string): string {
  return text.trim().replace(/\s+/g, ' ')
}

export function canSearch(term: string): boolean {
  return term.length >= SEARCH_MIN_LENGTH
}

export function resultCount(result: SearchResult | undefined): number {
  return result ? searchGroups(result).reduce((n, g) => n + g.count, 0) : 0
}

export type SearchGroupKey = 'clients' | 'projects' | 'quotations' | 'tasks' | 'requests'

export const searchGroupLabel: Record<SearchGroupKey, string> = {
  clients: 'Khách hàng',
  projects: 'Dự án',
  quotations: 'Báo giá',
  tasks: 'Công việc',
  requests: 'Yêu cầu',
}

/** The non-empty groups in display order; `tasks` / `requests` may be missing (older server). */
export function searchGroups(result: SearchResult): { key: SearchGroupKey; label: string; count: number }[] {
  const counts: Record<SearchGroupKey, number> = {
    clients: result.clients.length,
    projects: result.projects.length,
    quotations: result.quotations.length,
    tasks: result.tasks?.length ?? 0,
    requests: result.requests?.length ?? 0,
  }
  return (Object.keys(searchGroupLabel) as SearchGroupKey[])
    .filter(key => counts[key] > 0)
    .map(key => ({ key, label: searchGroupLabel[key], count: counts[key] }))
}

export function taskSearchHref(t: Pick<SearchTask, 'id' | 'projectId'>): string {
  return `/admin/projects/${t.projectId}?tab=tasks&task=${t.id}`
}

export function requestSearchHref(r: Pick<SearchRequest, 'id'>): string {
  return `/admin/requests/${r.id}`
}

export function clientHref(client: { id: string }): string {
  return `/admin/clients/${client.id}`
}

export function projectHref(project: { id: string }): string {
  return `/admin/projects/${project.id}`
}

export function quotationHref(quotation: { id: string }): string {
  return `/admin/quotations/${quotation.id}`
}

export function quotationLabel(q: Pick<SearchQuotation, 'number' | 'version'>): string {
  return q.number ? `${q.number} · v${q.version}` : 'Báo giá nháp'
}

export function shortcutLabel(platform: string): string {
  return /Mac|iPhone|iPad/i.test(platform) ? '⌘K' : 'Ctrl K'
}
