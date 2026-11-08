import type { SearchQuotation, SearchResult } from '@/lib/searchTypes'

/** Same bound as the API (SearchQuery.MinLength): shorter terms are not sent. */
export const SEARCH_MIN_LENGTH = 2

export function searchTerm(text: string): string {
  return text.trim().replace(/\s+/g, ' ')
}

export function canSearch(term: string): boolean {
  return term.length >= SEARCH_MIN_LENGTH
}

export function resultCount(result: SearchResult | undefined): number {
  return result ? result.clients.length + result.projects.length + result.quotations.length : 0
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
