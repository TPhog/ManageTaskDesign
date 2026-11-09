import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { AdminRequestTemplate, IntakeLink, RequestTemplateBody } from '@/lib/requestTemplateTypes'

export function useRequestTemplates() {
  return useQuery({ queryKey: qk.requestTemplates.all, queryFn: () => api.get<AdminRequestTemplate[]>('/admin/request-templates') })
}

export function useRequestTemplate(id?: string) {
  return useQuery({
    queryKey: qk.requestTemplates.detail(id ?? ''),
    queryFn: () => api.get<AdminRequestTemplate>(`/admin/request-templates/${id}`),
    enabled: !!id,
  })
}

export function useSaveRequestTemplate(id?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: RequestTemplateBody) => id
      ? api.put<AdminRequestTemplate>(`/admin/request-templates/${id}`, body)
      : api.post<AdminRequestTemplate>('/admin/request-templates', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.requestTemplates.all }),
  })
}

export function useDeleteRequestTemplate() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/request-templates/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.requestTemplates.all }),
  })
}

export function useReorderRequestTemplates() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (ids: string[]) => api.post<void>('/admin/request-templates/reorder', { ids }),
    onSettled: () => qc.invalidateQueries({ queryKey: qk.requestTemplates.all }),
  })
}

export function useIntakeLink() {
  return useQuery({ queryKey: qk.intakeLink, queryFn: () => api.get<IntakeLink>('/admin/settings/intake') })
}

export function useUpdateIntakeLink() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (enabled: boolean) => api.put<IntakeLink>('/admin/settings/intake', { enabled }),
    onSuccess: data => qc.setQueryData(qk.intakeLink, data),
  })
}

export function useRegenerateIntakeLink() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<IntakeLink>('/admin/settings/intake/regenerate', {}),
    onSuccess: data => qc.setQueryData(qk.intakeLink, data),
  })
}
