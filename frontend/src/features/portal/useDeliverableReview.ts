import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PortalDeliverable, PortalDeliverableDecision } from '@/lib/portalTypes'
import { approveErrorMessage } from './review'

/** "Duyệt": records the client's approval; the project page and its timeline refresh either way. */
export function useApproveDeliverable(projectId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (d: Pick<PortalDeliverable, 'id' | 'name'>) =>
      api.post<PortalDeliverableDecision>(`/portal/deliverables/${d.id}/approve`, {}),
    onSuccess: (_result, d) => toast.success(`Đã ghi nhận bạn duyệt ${d.name}`),
    onError: err => toast.error(approveErrorMessage(err)),
    // qk.portal.project(id) is a prefix of the timeline key, so both refetch
    onSettled: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: qk.portal.project(projectId) }),
        qc.invalidateQueries({ queryKey: qk.portal.projects }),
      ]),
  })
}
