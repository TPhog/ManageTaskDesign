import type { ApiError } from '@/lib/api'
import type { PortalDeliverable } from '@/lib/portalTypes'
import { portalDeliverableStatus } from './labels'

export type ReviewState = 'NONE' | 'AWAITING' | 'APPROVED_BY_CLIENT' | 'CHANGES_IN_PROGRESS'

/** Plan 12 BR-48: the server decides the one review state the client sees. */
export function reviewState(d: Pick<PortalDeliverable, 'reviewState'>): ReviewState {
  switch (d.reviewState) {
    case 'AWAITING_CLIENT': return 'AWAITING'
    case 'CLIENT_APPROVED': return 'APPROVED_BY_CLIENT'
    case 'CHANGES_IN_PROGRESS': return 'CHANGES_IN_PROGRESS'
    default: return 'NONE'
  }
}

/** CF-01/02: one badge per product — the review state wins over the raw status while the product is under review. */
export function deliverableBadge(d: Pick<PortalDeliverable, 'status' | 'reviewState'>): { status: string; label: string } {
  switch (reviewState(d)) {
    case 'AWAITING': return { status: 'IN_REVIEW', label: 'Chờ bạn duyệt' }
    case 'APPROVED_BY_CLIENT': return { status: 'APPROVED', label: 'Bạn đã duyệt — chờ xác nhận' }
    case 'CHANGES_IN_PROGRESS': return { status: 'IN_PROGRESS', label: 'Đang sửa theo yêu cầu của bạn' }
    default: return { status: d.status, label: portalDeliverableStatus[d.status] ?? d.status }
  }
}

export function progressText(d: Pick<PortalDeliverable, 'progressPercent'>): string | null {
  return d.progressPercent === null || d.progressPercent === undefined ? null : `Tiến độ ${d.progressPercent}%`
}

export function approveErrorMessage(err: unknown): string {
  const e = (err ?? {}) as Partial<ApiError>
  if (e.status === 409 && e.code === 'CHANGE_REQUEST_OPEN') {
    return 'Bạn đang có yêu cầu sửa cho sản phẩm này — chờ nhà thiết kế gửi bản mới rồi duyệt.'
  }
  if (e.status === 409) return 'Sản phẩm không còn chờ duyệt — trang đã được tải lại.'
  if (e.status === 404) return 'Không tìm thấy sản phẩm này.'
  return 'Không gửi được. Vui lòng thử lại.'
}
