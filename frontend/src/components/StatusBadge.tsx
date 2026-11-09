import { cn } from '@/lib/utils'

export type StatusTone = 'neutral' | 'active' | 'attention' | 'success' | 'warning' | 'danger'

// [badge, dot]. Text always carries the meaning (label); the tone only reinforces it (docs/08 §5).
const toneClass: Record<StatusTone, [string, string]> = {
  neutral: ['bg-surface-sunk text-muted-foreground ring-border', 'bg-muted-foreground'],
  active: ['bg-surface-hover text-foreground ring-border-strong', 'bg-foreground'],
  attention: ['bg-highlight-soft text-highlight-text ring-highlight/30', 'bg-highlight'],
  success: ['bg-success-soft text-success ring-success/25', 'bg-success'],
  warning: ['bg-warning-soft text-warning ring-warning/25', 'bg-warning'],
  danger: ['bg-danger-soft text-danger ring-danger/25', 'bg-danger'],
}

export const statusTone: Record<string, StatusTone> = {
  DRAFT: 'neutral', QUOTED: 'active', IN_PROGRESS: 'active', ON_HOLD: 'warning', COMPLETED: 'success', CANCELLED: 'danger',
  SENT: 'active', ACCEPTED: 'success', REJECTED: 'danger', EXPIRED: 'warning', SUPERSEDED: 'neutral',
  UNPAID: 'neutral', DEPOSIT_PAID: 'warning', PAID: 'success',
  NEW: 'attention', TRIAGED: 'success', DECLINED: 'danger',
  PENDING: 'neutral', CONFIRMED: 'success', ACTIVE: 'success', INACTIVE: 'neutral',
  NOT_STARTED: 'neutral', IN_REVIEW: 'attention', APPROVED: 'success', DELIVERED: 'success',
  TODO: 'neutral', REVIEW: 'attention', DONE: 'success', BLOCKED: 'danger',
}

const labelMap: Record<string, string> = {
  DRAFT: 'Nháp',
  QUOTED: 'Đã báo giá',
  IN_PROGRESS: 'Đang làm',
  ON_HOLD: 'Tạm dừng',
  COMPLETED: 'Hoàn thành',
  CANCELLED: 'Đã huỷ',
  SENT: 'Đã gửi',
  ACCEPTED: 'Đã chấp nhận',
  REJECTED: 'Từ chối',
  EXPIRED: 'Hết hạn',
  SUPERSEDED: 'Đã thay thế',
  UNPAID: 'Chưa thanh toán',
  DEPOSIT_PAID: 'Đã nhận cọc',
  PAID: 'Đã thanh toán',
  NEW: 'Mới',
  TRIAGED: 'Đã xem',
  DECLINED: 'Từ chối',
  PENDING: 'Chờ làm',
  CONFIRMED: 'Đã xác nhận',
  ACTIVE: 'Đang hoạt động',
  INACTIVE: 'Lưu trữ',
  NOT_STARTED: 'Chưa bắt đầu',
  IN_REVIEW: 'Chờ khách duyệt',
  APPROVED: 'Khách đã duyệt',
  DELIVERED: 'Đã bàn giao',
  TODO: 'Cần làm',
  REVIEW: 'Chờ duyệt',
  DONE: 'Xong',
  BLOCKED: 'Bị chặn',
}

export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const [cls, dot] = toneClass[statusTone[status] ?? 'neutral']
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        'editorial:rounded-none editorial:bg-transparent editorial:px-0 editorial:font-serif editorial:text-sm editorial:italic editorial:ring-0',
        cls,
        status === 'SUPERSEDED' && 'line-through',
      )}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full editorial:rounded-none', dot)} aria-hidden="true" />
      {label ?? labelMap[status] ?? status}
    </span>
  )
}
