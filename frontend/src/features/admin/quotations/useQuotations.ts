import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PagedResult } from '@/lib/types'
import type {
  CreateQuotationBody,
  CreateQuotationResult,
  PaymentStatus,
  QuotationDetail,
  QuotationListItem,
  QuotationListParams,
  UpdateQuotationBody,
} from '@/lib/quotationTypes'

function toSearch(params: QuotationListParams): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value))
  }
  return search.toString()
}

/** After a write: seed the detail cache with the server's answer, refresh lists and the project money summary. */
function useOnQuotationChanged() {
  const qc = useQueryClient()
  return (detail?: QuotationDetail) => {
    if (detail) qc.setQueryData(qk.quotations.detail(detail.id), detail)
    // Other versions' details (SENT → SUPERSEDED, draft links…) go stale too; only the just-seeded detail is fresh.
    qc.invalidateQueries({
      queryKey: qk.quotations.all,
      predicate: q => !detail || q.queryKey[1] !== detail.id,
    })
    qc.invalidateQueries({ queryKey: qk.projects.all })
    qc.invalidateQueries({ queryKey: qk.revisions.all }) // item→quotation links, quota and suggestions
    qc.invalidateQueries({ queryKey: qk.tasks.all }) // plan 09: accept may create template tasks
  }
}

export function useProjectQuotations(projectId: string) {
  return useQuery({
    queryKey: qk.quotations.list(projectId),
    queryFn: () => api.get<PagedResult<QuotationListItem>>(`/admin/quotations?projectId=${projectId}&pageSize=100`),
  })
}

export function useQuotationSearch(params: QuotationListParams) {
  return useQuery({
    queryKey: qk.quotations.search(params),
    queryFn: () => api.get<PagedResult<QuotationListItem>>(`/admin/quotations?${toSearch(params)}`),
  })
}

export function useQuotation(id: string | undefined) {
  return useQuery({
    queryKey: qk.quotations.detail(id ?? ''),
    queryFn: () => api.get<QuotationDetail>(`/admin/quotations/${id}`),
    enabled: !!id,
  })
}

export function useCreateQuotation(projectId: string) {
  const changed = useOnQuotationChanged()
  return useMutation({
    mutationFn: (body: CreateQuotationBody) =>
      api.post<CreateQuotationResult>(`/admin/projects/${projectId}/quotations`, body),
    onSuccess: () => changed(),
  })
}

export function useUpdateQuotation(id: string) {
  const changed = useOnQuotationChanged()
  return useMutation({
    mutationFn: (body: UpdateQuotationBody) => api.put<QuotationDetail>(`/admin/quotations/${id}`, body),
    onSuccess: detail => changed(detail),
  })
}

export function useDeleteQuotation(id: string) {
  const qc = useQueryClient()
  const changed = useOnQuotationChanged()
  return useMutation({
    mutationFn: () => api.delete<void>(`/admin/quotations/${id}`),
    onSuccess: () => {
      qc.removeQueries({ queryKey: qk.quotations.detail(id) })   // gone: don't refetch a 404
      changed()
    },
  })
}

export type QuotationCommand =
  | { action: 'send'; body: { rowVersion: number } }
  | { action: 'new-version'; body: Record<string, never> }
  | { action: 'accept'; body: { acceptedAt: string; note: string | null; rowVersion: number } }
  | { action: 'reject'; body: { rejectedAt: string; reason: string; rowVersion: number } }
  | { action: 'payment-status'; body: { to: PaymentStatus; date: string; reason: string | null; rowVersion: number } }

/** Every command answers with the resulting quotation (for new-version: the new DRAFT). */
export function useQuotationCommand(id: string) {
  const changed = useOnQuotationChanged()
  return useMutation({
    mutationFn: (cmd: QuotationCommand) => api.post<QuotationDetail>(`/admin/quotations/${id}/${cmd.action}`, cmd.body),
    onSuccess: detail => changed(detail),
  })
}
