import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Dashboard } from '@/lib/dashboardTypes'
import { qk } from '@/lib/queryKeys'

/** GET /api/admin/dashboard (docs/05 §3.12). staleTime 0: the numbers summarise every other page, refetch on each visit. */
export function useDashboard() {
  return useQuery({
    queryKey: qk.dashboard,
    queryFn: () => api.get<Dashboard>('/admin/dashboard'),
    staleTime: 0,
  })
}
