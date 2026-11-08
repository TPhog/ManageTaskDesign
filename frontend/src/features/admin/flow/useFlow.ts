import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { ClientDetail, DeliverableStatus, ProjectDetail } from '@/lib/types'
import type { QuotationDetail } from '@/lib/quotationTypes'
import type { FlowProjectActions, FlowState, StartPreviewItem, StartProjectBody, StartProjectResult } from '@/lib/flowTypes'
import { paymentReminderMessage, quotationSentMessage } from '@/lib/messageTemplates'
import { flowErrorText } from './flowUtils'

/** Flow, project detail (deliverable progress), task lists and revisions all derive from the same rows. */
function useRefreshProjectData() {
  const qc = useQueryClient()
  return () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: qk.projects.all }),
      qc.invalidateQueries({ queryKey: qk.tasks.lists }),
      qc.invalidateQueries({ queryKey: qk.revisions.all }),
    ])
}

export function useProjectFlow(projectId: string | undefined) {
  return useQuery({
    queryKey: qk.flow.project(projectId ?? ''),
    queryFn: () => api.get<FlowState>(`/admin/projects/${projectId}/flow`),
    enabled: !!projectId,
  })
}

export function useStartPreview(projectId: string, enabled: boolean) {
  return useQuery({
    queryKey: qk.flow.startPreview(projectId),
    queryFn: () => api.get<StartPreviewItem[]>(`/admin/projects/${projectId}/start-preview`),
    enabled,
    // the form is seeded once per open: a refetch must not be able to discard the designer's edits
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnMount: 'always',
  })
}

export function useStartProject(projectId: string) {
  const refresh = useRefreshProjectData()
  return useMutation({
    mutationFn: (body: StartProjectBody) => api.post<StartProjectResult>(`/admin/projects/${projectId}/start`, body),
    onSuccess: result => {
      toast.success(`Đã bắt đầu dự án · tạo ${result.createdTasks} công việc`)
      if (result.warnings.length > 0) toast.warning(result.warnings.join(' · '))
      return refresh()
    },
    onError: (err: ApiError) => toast.error(flowErrorText(err, 'Không thể bắt đầu dự án')),
  })
}

export function useSubmitReview() {
  const refresh = useRefreshProjectData()
  return useMutation({
    mutationFn: ({ deliverableId, clientMessage }: { deliverableId: string; clientMessage?: string }) =>
      api.post<unknown>(`/admin/deliverables/${deliverableId}/submit-review`, { clientMessage: clientMessage ?? null }),
    onSuccess: () => {
      toast.success('Đã gửi khách duyệt')
      return refresh()
    },
    onError: (err: ApiError) => toast.error(flowErrorText(err, 'Không thể gửi duyệt')),
  })
}

export function useConfirmApproval() {
  const refresh = useRefreshProjectData()
  return useMutation({
    mutationFn: (deliverableId: string) => api.post<unknown>(`/admin/deliverables/${deliverableId}/confirm-approval`, {}),
    onSuccess: () => {
      toast.success('Đã xác nhận khách duyệt')
      return refresh()
    },
    onError: (err: ApiError) => toast.error(flowErrorText(err, 'Không thể xác nhận duyệt')),
  })
}

export function useDeliverableStatus() {
  const refresh = useRefreshProjectData()
  return useMutation({
    mutationFn: ({ deliverableId, to }: { deliverableId: string; to: DeliverableStatus }) =>
      api.post<unknown>(`/admin/deliverables/${deliverableId}/status`, { to }),
    onSuccess: () => {
      toast.success('Đã cập nhật trạng thái')
      return refresh()
    },
    onError: (err: ApiError) => toast.error(flowErrorText(err, 'Không thể cập nhật trạng thái')),
  })
}

/** COPY_QUOTATION_MESSAGE / COPY_PAYMENT_REMINDER: build the text from the quotation and put it on the clipboard. */
export function useCopyQuotationText() {
  const qc = useQueryClient()
  return async (kind: 'COPY_QUOTATION_MESSAGE' | 'COPY_PAYMENT_REMINDER', quotationId: string) => {
    try {
      const q = await qc.fetchQuery({
        queryKey: qk.quotations.detail(quotationId),
        queryFn: () => api.get<QuotationDetail>(`/admin/quotations/${quotationId}`),
      })
      if (kind === 'COPY_QUOTATION_MESSAGE') {
        // the "old link" wording only makes sense when the client has a usable portal link
        const project = await qc.fetchQuery({
          queryKey: qk.projects.detail(q.projectId),
          queryFn: () => api.get<ProjectDetail>(`/admin/projects/${q.projectId}`),
        })
        const client = await qc.fetchQuery({
          queryKey: qk.clients.detail(project.client.id),
          queryFn: () => api.get<ClientDetail>(`/admin/clients/${project.client.id}`),
        })
        if (!client.hasPortalLink || !client.portalEnabled) {
          toast.warning(client.hasPortalLink
            ? 'Portal của khách đang tắt — bật lại ở Khách hàng trước khi gửi tin nhắn.'
            : 'Khách chưa có link portal — mở báo giá → "Tin nhắn mẫu" để tạo link và sao chép tin nhắn.')
          return
        }
      }
      await navigator.clipboard.writeText(kind === 'COPY_QUOTATION_MESSAGE' ? quotationSentMessage(q, null) : paymentReminderMessage(q))
      toast.success('Đã sao chép tin nhắn — dán vào Zalo cho khách')
    } catch {
      toast.error('Không sao chép được — mở báo giá để sao chép thủ công')
    }
  }
}

/** Dashboard "Việc cần làm": stage, actions and warnings of every open project (GET /admin/flow/actions). */
export function useFlowActions() {
  return useQuery({
    queryKey: qk.flow.actions,
    queryFn: () => api.get<FlowProjectActions[]>('/admin/flow/actions'),
    staleTime: 0,
  })
}
