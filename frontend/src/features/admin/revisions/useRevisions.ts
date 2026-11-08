import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { CreateQuotationResult } from '@/lib/quotationTypes'
import type { PagedResult, TaskDetail } from '@/lib/types'
import type {
  ActivityEntry,
  BillingSuggestion,
  ClientRequest,
  CreateAdditionalQuotationBody,
  CreateItemTaskBody,
  CreateRequestBody,
  ProjectRevisions,
  RequestDetail,
  RequestStatus,
  RevisionItem,
  RevisionItemType,
  RevisionRound,
  RoundInput,
  TriageBody,
  UpdateRevisionItemBody,
  WorkStatusBody,
} from '@/lib/revisionTypes'

/** Anything that changes rounds/items: revisions tab, request inbox/details, quota meters, client timeline. */
function useOnRevisionsChanged() {
  const qc = useQueryClient()
  return (projectId: string) => {
    qc.invalidateQueries({ queryKey: qk.revisions.project(projectId) })
    qc.invalidateQueries({ queryKey: qk.revisions.progress(projectId) })
    qc.invalidateQueries({ queryKey: qk.projects.detail(projectId) })
    qc.invalidateQueries({ queryKey: qk.requests.all })
  }
}

export function useRequests(status: RequestStatus) {
  return useQuery({
    queryKey: qk.requests.list(status),
    queryFn: () => api.get<PagedResult<ClientRequest>>(`/admin/requests?status=${status}&pageSize=100`),
  })
}

/** REQ-03 sidebar badge */
export function useNewRequestCount() {
  return useQuery({
    queryKey: qk.requests.newCount,
    queryFn: () => api.get<{ count: number }>('/admin/requests/new-count'),
    refetchInterval: 60_000,
  })
}

export function useRequest(id: string | undefined) {
  return useQuery({
    queryKey: qk.requests.detail(id ?? ''),
    queryFn: () => api.get<RequestDetail>(`/admin/requests/${id}`),
    enabled: !!id,
  })
}

export function useCreateRequest() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ projectId, body }: { projectId: string; body: CreateRequestBody }) =>
      api.post<RequestDetail>(`/admin/projects/${projectId}/requests`, body),
    onSuccess: detail => {
      qc.setQueryData(qk.requests.detail(detail.request.id), detail)
      qc.invalidateQueries({ queryKey: qk.requests.all })
      qc.invalidateQueries({ queryKey: qk.projects.detail(detail.request.projectId) })
    },
  })
}

export function useTriageRequest(id: string) {
  const qc = useQueryClient()
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: (body: TriageBody) => api.post<RequestDetail>(`/admin/requests/${id}/triage`, body),
    onSuccess: detail => {
      qc.setQueryData(qk.requests.detail(id), detail)
      changed(detail.request.projectId)
    },
  })
}

export function useDeclineRequest(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { reason: string; rowVersion: number }) =>
      api.post<RequestDetail>(`/admin/requests/${id}/decline`, body),
    onSuccess: detail => {
      qc.setQueryData(qk.requests.detail(id), detail)
      qc.invalidateQueries({ queryKey: qk.requests.all })
      qc.invalidateQueries({ queryKey: qk.revisions.progress(detail.request.projectId) })
    },
  })
}

/** docs/08 §2.3 step 2: billing the server would give a new round of these item types (saves nothing) */
export function useSuggestBilling(deliverableId: string | null, types: RevisionItemType[]) {
  return useQuery({
    queryKey: qk.revisions.suggest(deliverableId, types),
    queryFn: () => api.post<BillingSuggestion[]>('/admin/revisions/suggest-billing', {
      deliverableId,
      items: types.map(type => ({ type })),
    }),
    enabled: types.length > 0,
    staleTime: 30_000,
  })
}

export function useProjectRevisions(projectId: string) {
  return useQuery({
    queryKey: qk.revisions.project(projectId),
    queryFn: () => api.get<ProjectRevisions>(`/admin/projects/${projectId}/revisions`),
  })
}

export function useCreateRound(projectId: string) {
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: (body: RoundInput) => api.post<RevisionRound>(`/admin/projects/${projectId}/rounds`, body),
    onSuccess: () => changed(projectId),
  })
}

export function useUpdateRevisionItem(projectId: string) {
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateRevisionItemBody }) =>
      api.put<RevisionItem>(`/admin/revision-items/${id}`, body),
    onSuccess: () => changed(projectId),
  })
}

export function useChangeWorkStatus(projectId: string) {
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: WorkStatusBody }) =>
      api.post<RevisionItem>(`/admin/revision-items/${id}/work-status`, body),
    onSuccess: () => changed(projectId),
  })
}

export function useCreateItemTask(projectId: string) {
  const qc = useQueryClient()
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CreateItemTaskBody }) =>
      api.post<TaskDetail>(`/admin/revision-items/${id}/tasks`, body),
    onSuccess: () => {
      changed(projectId)
      qc.invalidateQueries({ queryKey: qk.tasks.lists })
    },
  })
}

export function useCreateAdditionalQuotation(projectId: string) {
  const qc = useQueryClient()
  const changed = useOnRevisionsChanged()
  return useMutation({
    mutationFn: (body: CreateAdditionalQuotationBody) =>
      api.post<CreateQuotationResult>(`/admin/projects/${projectId}/quotations/from-revisions`, body),
    onSuccess: () => {
      changed(projectId)
      qc.invalidateQueries({ queryKey: qk.quotations.all })
    },
  })
}

/** What the client currently sees on the portal timeline (activity with visibleToClient + clientMessage) */
export function useClientTimeline(projectId: string) {
  return useQuery({
    queryKey: qk.revisions.progress(projectId),
    queryFn: () => api.get<PagedResult<ActivityEntry>>(`/admin/projects/${projectId}/activity?pageSize=50`),
    select: page => page.items.filter(a => a.visibleToClient && a.clientMessage),
  })
}

export function usePostProgressUpdate(projectId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (message: string) =>
      api.post<{ occurredAt: string; message: string }>(`/admin/projects/${projectId}/progress-updates`, { message }),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.revisions.progress(projectId) }),
  })
}
