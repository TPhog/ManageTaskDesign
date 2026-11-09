import { formatVnd } from '@/lib/format'
import type { PortalProjectListItem, PortalQuotationListItem } from '@/lib/portalTypes'

export interface TodoItem {
  key: string
  kind: 'QUOTATION' | 'PAYMENT' | 'REVIEW'
  label: string
  to: string
}

/**
 * BR-75/77: the amount the client should transfer now, or null. A quotation waits for money once the client agreed
 * (or the designer recorded it as accepted); the remaining amount is asked only after something was delivered.
 * A transfer the client already reported (pending) is not asked again; an expired quotation asks for nothing.
 */
function paymentDue(q: PortalQuotationListItem, delivered: boolean): string | null {
  if (q.effectiveStatus === 'EXPIRED' || q.clientPaymentNotice?.pending) return null
  const agreed = (q.effectiveStatus === 'SENT' && !!q.clientAcceptedAt) || q.effectiveStatus === 'ACCEPTED'
  if (!agreed) return null
  if (q.paymentStatus === 'UNPAID') {
    return q.depositAmount > 0
      ? `Chuyển tiền cọc ${formatVnd(q.depositAmount)} (${q.number})`
      : `Thanh toán ${formatVnd(q.total)} (${q.number})`
  }
  if (q.effectiveStatus === 'ACCEPTED' && q.paymentStatus === 'DEPOSIT_PAID' && delivered) {
    return `Chuyển phần còn lại ${formatVnd(q.remainingAmount)} (${q.number})`
  }
  return null
}

/** CF-14 "Việc bạn cần làm": agree to quotations, then pay, then review products. */
export function buildTodoList(projects: PortalProjectListItem[], quotations: PortalQuotationListItem[]): TodoItem[] {
  const deliveredIn = new Set(projects.filter(p => p.deliverableSummary.delivered > 0).map(p => p.id))
  const agree: TodoItem[] = quotations
    .filter(q => q.status === 'SENT' && q.effectiveStatus === 'SENT' && !q.clientAcceptedAt)
    .map(q => ({ key: `quotation-${q.id}`, kind: 'QUOTATION', label: `Xem và đồng ý báo giá ${q.number}`, to: `/portal/quotations/${q.id}` }))
  const pay: TodoItem[] = quotations.flatMap(q => {
    const label = paymentDue(q, deliveredIn.has(q.projectId))
    return label ? [{ key: `payment-${q.id}`, kind: 'PAYMENT' as const, label, to: `/portal/quotations/${q.id}` }] : []
  })
  const review: TodoItem[] = projects
    .filter(p => p.deliverableSummary.awaitingClient > 0)
    .map(p => ({
      key: `review-${p.id}`, kind: 'REVIEW', label: `Duyệt ${p.deliverableSummary.awaitingClient} sản phẩm của ${p.name}`,
      to: `/portal/projects/${p.id}`,
    }))
  return [...agree, ...pay, ...review]
}
