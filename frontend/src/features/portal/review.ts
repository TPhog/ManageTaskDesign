import type { PortalDeliverable } from '@/lib/portalTypes'

export type ReviewState = 'NONE' | 'AWAITING' | 'APPROVED_BY_CLIENT'

/** Plan 06b F2: the client decides only while the deliverable is IN_REVIEW. */
export function reviewState(d: Pick<PortalDeliverable, 'status' | 'clientDecision'>): ReviewState {
  if (d.status !== 'IN_REVIEW') return 'NONE'
  return d.clientDecision === 'APPROVED' ? 'APPROVED_BY_CLIENT' : 'AWAITING'
}

export function progressText(d: Pick<PortalDeliverable, 'progressPercent'>): string | null {
  return d.progressPercent === null || d.progressPercent === undefined ? null : `Tiến độ ${d.progressPercent}%`
}

export function approveErrorMessage(err: unknown): string {
  const status = (err as { status?: number } | null)?.status
  if (status === 409) return 'Hạng mục không còn chờ duyệt — trang đã được tải lại.'
  if (status === 404) return 'Không tìm thấy hạng mục này.'
  return 'Không gửi được. Vui lòng thử lại.'
}
