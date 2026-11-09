import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PortalQuotationDetail } from '@/lib/portalTypes'
import type { QrPurpose } from '@/lib/quotationTypes'
import { statementErrorMessage } from './quotation'

/**
 * ['portal','quotations'] prefixes the list and detail keys; qk.portal.projects prefixes project detail and timeline —
 * so the quotation list, "Việc bạn cần làm" and "Hoạt động gần đây" refresh after a statement.
 */
function useRefreshAfterStatement() {
  const qc = useQueryClient()
  return () => Promise.all([
    qc.invalidateQueries({ queryKey: ['portal', 'quotations'] }),
    qc.invalidateQueries({ queryKey: qk.portal.projects }),
  ])
}

/** CF-03: "Đồng ý báo giá" — recorded only (BR-01/76); the designer confirms. */
export function useAcceptQuotation(id: string) {
  const qc = useQueryClient()
  const refresh = useRefreshAfterStatement()
  return useMutation({
    mutationFn: () => api.post<PortalQuotationDetail>(`/portal/quotations/${id}/accept`, {}),
    onSuccess: q => { qc.setQueryData(qk.portal.quotation(id), q); toast.success('Đã ghi nhận bạn đồng ý báo giá') },
    onError: err => toast.error(statementErrorMessage(err)),
    onSettled: refresh,
  })
}

/** CF-03: "Tôi đã chuyển khoản" — recorded only (BR-77); the designer checks the account and confirms. */
export function usePaymentNotice(id: string) {
  const qc = useQueryClient()
  const refresh = useRefreshAfterStatement()
  return useMutation({
    mutationFn: (body: { purpose: QrPurpose; note: string | null }) =>
      api.post<PortalQuotationDetail>(`/portal/quotations/${id}/payment-notice`, body),
    onSuccess: q => { qc.setQueryData(qk.portal.quotation(id), q); toast.success('Đã báo nhà thiết kế — chờ xác nhận đã nhận tiền') },
    onError: err => toast.error(statementErrorMessage(err)),
    onSettled: refresh,
  })
}
