import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PortalImage } from '@/lib/imageTypes'
import type {
  PortalMe,
  PortalProjectDetail,
  PortalProjectListItem,
  PortalQuotationDetail,
  PortalQuotationListItem,
  PortalRequest,
  PortalRevisions,
} from '@/lib/portalTypes'
import { toRequestForm, type RequestFormValues } from './requests'

const byProject = (projectId?: string) => (projectId ? `?projectId=${encodeURIComponent(projectId)}` : '')

/** Same key and fetcher as PortalLayout, so pages read the layout's cached session. */
export function usePortalMe() {
  return useQuery({ queryKey: qk.portal.me, queryFn: () => api.get<PortalMe>('/portal/me'), retry: false })
}

export function usePortalProjects() {
  return useQuery({ queryKey: qk.portal.projects, queryFn: () => api.get<PortalProjectListItem[]>('/portal/projects') })
}

export function usePortalProject(id?: string) {
  return useQuery({
    queryKey: qk.portal.project(id ?? ''),
    queryFn: () => api.get<PortalProjectDetail>(`/portal/projects/${id}`),
    enabled: !!id,
    retry: false,
  })
}

export function usePortalQuotations(projectId?: string, enabled = true) {
  return useQuery({
    queryKey: qk.portal.quotations(projectId),
    queryFn: () => api.get<PortalQuotationListItem[]>(`/portal/quotations${byProject(projectId)}`),
    enabled,
  })
}

/** Without `version`: the visible quotation; with it: that version of the same chain. */
export function usePortalQuotation(id: string, version?: number) {
  return useQuery({
    queryKey: version ? qk.portal.quotationVersion(id, version) : qk.portal.quotation(id),
    queryFn: () =>
      api.get<PortalQuotationDetail>(version ? `/portal/quotations/${id}/versions/${version}` : `/portal/quotations/${id}`),
    enabled: !!id,
    retry: false,
  })
}

export function usePortalRequests(projectId?: string, enabled = true) {
  return useQuery({
    queryKey: qk.portal.requests(projectId),
    queryFn: () => api.get<PortalRequest[]>(`/portal/requests${byProject(projectId)}`),
    enabled,
  })
}

export function useSendPortalRequest() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ files = [], ...values }: RequestFormValues & { files?: File[] }) =>
      api.postForm<PortalRequest>(`/portal/projects/${values.projectId}/requests`, toRequestForm(values, files)),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['portal', 'requests'] }),
  })
}

export function usePortalImages(projectId: string, enabled = true) {
  return useQuery({
    queryKey: qk.portal.images(projectId),
    queryFn: () => api.get<PortalImage[]>(`/portal/projects/${projectId}/images`),
    enabled: enabled && !!projectId,
  })
}

export function usePortalRevisions(projectId: string, enabled = true) {
  return useQuery({
    queryKey: qk.portal.revisions(projectId),
    queryFn: () => api.get<PortalRevisions>(`/portal/projects/${projectId}/revisions`),
    enabled: enabled && !!projectId,
  })
}
