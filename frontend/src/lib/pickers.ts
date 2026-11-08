import { useQuery } from '@tanstack/react-query'
import type { ComboOption } from '@/components/SearchCombobox'
import { api } from './api'
import type { ClientListItem, PagedResult, ProjectListItem } from './types'
import { useDebouncedValue } from './useDebouncedValue'

/** QA D-07: active clients for a picker (pass includeArchived for list filters, which must reach archived clients too) — newest first, searched on the server by name/company/phone. */
export function useClientOptions(search: string, enabled = true, includeArchived = false) {
  const q = useDebouncedValue(search.trim())
  const params = new URLSearchParams({ sort: 'createdAt:desc', pageSize: '20' })
  if (!includeArchived) params.set('status', 'ACTIVE')
  if (q) params.set('q', q)
  const query = useQuery({
    queryKey: ['clients', 'picker', includeArchived, q],
    queryFn: () => api.get<PagedResult<ClientListItem>>(`/admin/clients?${params}`),
    enabled,
  })
  const options: ComboOption[] = (query.data?.items ?? []).map(c => ({ value: c.id, label: c.name, hint: c.company ?? undefined }))
  return { options, loading: query.isLoading, total: query.data?.total ?? 0 }
}

/** QA D-07: projects for a picker — newest first, searched by project or client name; CANCELLED left out. */
export function useProjectOptions(search: string, enabled = true) {
  const q = useDebouncedValue(search.trim())
  const params = new URLSearchParams({ sort: 'createdAt:desc', pageSize: '20' })
  if (q) params.set('q', q)
  const query = useQuery({
    queryKey: ['projects', 'picker', q],
    queryFn: () => api.get<PagedResult<ProjectListItem>>(`/admin/projects?${params}`),
    enabled,
  })
  const options: ComboOption[] = (query.data?.items ?? [])
    .filter(p => p.status !== 'CANCELLED')
    .map(p => ({ value: p.id, label: p.name, hint: p.client.name }))
  return { options, loading: query.isLoading }
}
