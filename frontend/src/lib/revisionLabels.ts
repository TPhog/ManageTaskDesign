import type { Billing, DesignerRequestSource, RequestSource, RequestStatus, RevisionItemType } from './revisionTypes'

/** docs/08 §1 BillingChip */
export const billingLabel: Record<Billing, string> = {
  FREE_FIX: 'Sửa lỗi – miễn phí',
  IN_QUOTA: 'Trong quota',
  CHARGEABLE: 'Tính phí',
}

export const itemTypeLabel: Record<RevisionItemType, string> = {
  MINOR: 'Sửa nhỏ',
  MAJOR: 'Sửa lớn',
  SCOPE_CHANGE: 'Đổi phạm vi',
  NEW_DELIVERABLE: 'Hạng mục mới',
}

export const requestSourceLabel: Record<RequestSource, string> = {
  PORTAL: 'Portal',
  ZALO: 'Zalo',
  EMAIL: 'Email',
  PHONE: 'Điện thoại',
  OTHER: 'Khác',
}

/** Admin wording (docs/08 §2.2 inbox tabs); StatusBadge keeps the colours */
export const requestStatusLabel: Record<RequestStatus, string> = {
  NEW: 'Mới',
  TRIAGED: 'Đã xử lý',
  DECLINED: 'Từ chối',
}

export const designerSources: DesignerRequestSource[] = ['ZALO', 'EMAIL', 'PHONE', 'OTHER']

export type QuotaTone = 'ok' | 'warn' | 'over'

/** docs/08 §1 QuotaMeter: amber when exactly one free round is left, red when none is left */
export function quotaTone(used: number, total: number): QuotaTone {
  const remaining = total - used
  if (remaining <= 0) return 'over'
  return remaining === 1 ? 'warn' : 'ok'
}
