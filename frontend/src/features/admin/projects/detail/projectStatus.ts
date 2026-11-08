import type { ApiError } from '@/lib/api'
import type { ProjectStatus } from '@/lib/types'

export interface ProjectTransition {
  to: ProjectStatus
  label: string
  /** docs/02 §3: ON_HOLD, CANCELLED and reopening a COMPLETED project require a reason */
  reason: 'none' | 'hold' | 'cancel' | 'reopen'
  destructive?: boolean
}

const cancel: ProjectTransition = { to: 'CANCELLED', label: 'Huỷ dự án', reason: 'cancel', destructive: true }

/** Shared by the status menu and the COMPLETE_PROJECT flow action so both show the same warnings dialog. */
export const completeTransition: ProjectTransition = { to: 'COMPLETED', label: 'Hoàn thành', reason: 'none' }

/** docs/02 §3 — mirrors Project.ValidateTransition; CANCELLED is final. */
export const projectTransitions: Record<ProjectStatus, ProjectTransition[]> = {
  DRAFT: [
    { to: 'QUOTED', label: 'Chuyển sang Đã báo giá', reason: 'none' },
    { to: 'IN_PROGRESS', label: 'Bắt đầu làm', reason: 'none' },
    cancel,
  ],
  QUOTED: [{ to: 'IN_PROGRESS', label: 'Bắt đầu làm', reason: 'none' }, cancel],
  IN_PROGRESS: [
    { to: 'ON_HOLD', label: 'Tạm dừng', reason: 'hold' },
    completeTransition,
    cancel,
  ],
  ON_HOLD: [{ to: 'IN_PROGRESS', label: 'Tiếp tục làm', reason: 'none' }, cancel],
  COMPLETED: [{ to: 'IN_PROGRESS', label: 'Mở lại dự án', reason: 'reopen' }],
  CANCELLED: [],
}

export const reasonCopy: Record<Exclude<ProjectTransition['reason'], 'none'>, { title: string; description: string; label: string }> = {
  hold: {
    title: 'Tạm dừng',
    description: 'Ghi lý do tạm dừng. Bạn chọn được có hiện lý do này cho khách trên portal hay không.',
    label: 'Lý do tạm dừng',
  },
  cancel: {
    title: 'Huỷ dự án',
    description: 'Dự án đã huỷ không mở lại được. Khách vẫn xem portal nhưng không gửi yêu cầu mới.',
    label: 'Lý do huỷ',
  },
  reopen: {
    title: 'Mở lại dự án',
    description: 'Dự án quay lại Đang làm.',
    label: 'Lý do mở lại',
  },
}

/** QA D-01: a 422 BUSINESS_RULE with warnings → what to show for confirmation (docs/05 §3.4); otherwise null. */
export function confirmableWarnings(err: Pick<ApiError, 'status' | 'code' | 'warnings'>): string[] | null {
  return err.status === 422 && err.code === 'BUSINESS_RULE' && err.warnings && err.warnings.length > 0 ? err.warnings : null
}

/** Vietnamese toast for a status change that failed for another reason. */
export function statusErrorMessage(err: Pick<ApiError, 'status' | 'code' | 'detail' | 'errors'>): string {
  if (err.code === 'INVALID_TRANSITION') return 'Chuyển trạng thái không hợp lệ — tải lại trang để xem trạng thái mới nhất'
  if (err.code === 'CONCURRENCY_CONFLICT') return 'Dữ liệu đã thay đổi, vui lòng tải lại'
  const fieldError = err.errors ? Object.values(err.errors)[0]?.[0] : undefined
  return fieldError ?? err.detail ?? 'Không thể cập nhật trạng thái'
}
