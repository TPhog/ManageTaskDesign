import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { ConvertIntakeBody, ConvertIntakeResult, IntakeDetail, IntakePage, IntakeStatus } from '@/lib/requestTemplateTypes'

export const INTAKE_PAGE_SIZE = 20

export function useIntakeList(status: IntakeStatus, page: number) {
  return useQuery({
    queryKey: qk.intake.list(status, page),
    queryFn: () => api.get<IntakePage>(`/admin/intake?status=${status}&page=${page}&pageSize=${INTAKE_PAGE_SIZE}`),
  })
}

/** Sidebar badge next to "Khách mới" (same cadence as the requests badge). */
export function useNewIntakeCount() {
  return useQuery({ queryKey: qk.intake.newCount, queryFn: () => api.get<{ count: number }>('/admin/intake/new-count'), refetchInterval: 60_000 })
}

export function useIntake(id: string | undefined) {
  return useQuery({ queryKey: qk.intake.detail(id ?? ''), queryFn: () => api.get<IntakeDetail>(`/admin/intake/${id}`), enabled: !!id })
}

export function useConvertIntake(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ConvertIntakeBody) => api.post<ConvertIntakeResult>(`/admin/intake/${id}/convert`, body),
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: qk.intake.all }),
      qc.invalidateQueries({ queryKey: qk.clients.all }),
      qc.invalidateQueries({ queryKey: qk.projects.all }),
    ]),
  })
}

export function useRejectIntake(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { reason: string | null; rowVersion: number }) => api.post<IntakeDetail>(`/admin/intake/${id}/reject`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.intake.all }),
  })
}

export function useDeleteIntake() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/intake/${id}`),
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: qk.intake.all }),
      qc.invalidateQueries({ queryKey: qk.storage.all }),
    ]),
  })
}
