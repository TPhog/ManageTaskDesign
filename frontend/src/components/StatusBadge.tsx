// Each status: [badge classes, dot color]. Text colors are the -700 shade for ≥ 4.5:1 on the -50 tint.
const styleMap: Record<string, [string, string]> = {
  DRAFT: ['bg-slate-100 text-slate-700 ring-slate-200', 'bg-slate-400'],
  QUOTED: ['bg-violet-50 text-violet-700 ring-violet-200', 'bg-violet-500'],
  IN_PROGRESS: ['bg-blue-50 text-blue-700 ring-blue-200', 'bg-blue-500'],
  ON_HOLD: ['bg-amber-50 text-amber-700 ring-amber-200', 'bg-amber-500'],
  COMPLETED: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  CANCELLED: ['bg-red-50 text-red-700 ring-red-200', 'bg-red-500'],
  SENT: ['bg-blue-50 text-blue-700 ring-blue-200', 'bg-blue-500'],
  ACCEPTED: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  REJECTED: ['bg-red-50 text-red-700 ring-red-200', 'bg-red-500'],
  EXPIRED: ['bg-amber-50 text-amber-700 ring-amber-200', 'bg-amber-500'],
  SUPERSEDED: ['bg-slate-100 text-slate-500 ring-slate-200 line-through', 'bg-slate-300'],
  NEW: ['bg-blue-50 text-blue-700 ring-blue-200', 'bg-blue-500'],
  TRIAGED: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  DECLINED: ['bg-red-50 text-red-700 ring-red-200', 'bg-red-500'],
  PENDING: ['bg-slate-100 text-slate-700 ring-slate-200', 'bg-slate-400'],
  CONFIRMED: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  ACTIVE: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  INACTIVE: ['bg-slate-100 text-slate-600 ring-slate-200', 'bg-slate-400'],
  NOT_STARTED: ['bg-slate-100 text-slate-700 ring-slate-200', 'bg-slate-400'],
  IN_REVIEW: ['bg-amber-50 text-amber-700 ring-amber-200', 'bg-amber-500'],
  APPROVED: ['bg-teal-50 text-teal-700 ring-teal-200', 'bg-teal-500'],
  DELIVERED: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  TODO: ['bg-slate-100 text-slate-700 ring-slate-200', 'bg-slate-400'],
  REVIEW: ['bg-amber-50 text-amber-700 ring-amber-200', 'bg-amber-500'],
  DONE: ['bg-emerald-50 text-emerald-700 ring-emerald-200', 'bg-emerald-500'],
  BLOCKED: ['bg-red-50 text-red-700 ring-red-200', 'bg-red-500'],
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
  const [cls, dot] = styleMap[status] ?? styleMap.DRAFT
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${cls}`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${dot}`} aria-hidden="true" />
      {label ?? labelMap[status] ?? status}
    </span>
  )
}
