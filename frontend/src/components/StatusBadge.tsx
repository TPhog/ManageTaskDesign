const colorMap: Record<string, string> = {
  DRAFT: 'bg-gray-100 text-gray-600',
  QUOTED: 'bg-purple-100 text-purple-700',
  IN_PROGRESS: 'bg-blue-100 text-blue-700',
  ON_HOLD: 'bg-orange-100 text-orange-700',
  COMPLETED: 'bg-green-100 text-green-700',
  CANCELLED: 'bg-red-100 text-red-500',
  SENT: 'bg-blue-100 text-blue-700',
  ACCEPTED: 'bg-green-100 text-green-700',
  REJECTED: 'bg-red-100 text-red-500',
  EXPIRED: 'bg-orange-100 text-orange-700',
  SUPERSEDED: 'bg-gray-100 text-gray-400 line-through',
  NEW: 'bg-blue-100 text-blue-700',
  TRIAGED: 'bg-green-100 text-green-700',
  DECLINED: 'bg-red-100 text-red-500',
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
}

export function StatusBadge({ status }: { status: string }) {
  const cls = colorMap[status] ?? 'bg-gray-100 text-gray-600'
  const label = labelMap[status] ?? status
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {label}
    </span>
  )
}
