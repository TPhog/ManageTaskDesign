// Client-facing wording (docs/08 §3.3): no internal terms such as "deliverable" or "round".

export const portalProjectStatus: Record<string, string> = {
  DRAFT: 'Đang chuẩn bị',
  QUOTED: 'Đã báo giá',
  IN_PROGRESS: 'Đang thực hiện',
  ON_HOLD: 'Tạm dừng',
  COMPLETED: 'Hoàn thành',
  CANCELLED: 'Đã huỷ',
}

export const portalDeliverableStatus: Record<string, string> = {
  NOT_STARTED: 'Chưa bắt đầu',
  IN_PROGRESS: 'Đang thực hiện',
  IN_REVIEW: 'Chờ bạn duyệt',
  APPROVED: 'Bạn đã duyệt',
  DELIVERED: 'Đã bàn giao',
  CANCELLED: 'Đã huỷ',
}

export const designTypeLabel: Record<string, string> = {
  LOGO: 'Logo',
  BANNER: 'Banner',
  POSTER: 'Poster',
  BRANDING: 'Thương hiệu',
  UI_UX: 'UI/UX',
  SOCIAL_MEDIA: 'Mạng xã hội',
  PACKAGING: 'Bao bì',
  PRINT: 'In ấn',
  OTHER: 'Khác',
}

// docs/08 §3.2–3.3: client-facing names; EXPIRED stays a badge ("Đã hết hạn"), never a stored status.
export const portalQuotationStatus: Record<string, string> = {
  SENT: 'Chờ bạn xem xét',
  ACCEPTED: 'Đã xác nhận',
  REJECTED: 'Đã từ chối',
  EXPIRED: 'Đã hết hạn',
  SUPERSEDED: 'Phiên bản cũ',
}

export const portalPaymentStatus: Record<string, string> = {
  UNPAID: 'Chưa thanh toán',
  DEPOSIT_PAID: 'Đã nhận cọc',
  PAID: 'Đã thanh toán',
}

export const portalRequestStatus: Record<string, string> = {
  NEW: 'Mới',
  TRIAGED: 'Đã tiếp nhận',
  DECLINED: 'Từ chối',
}

export const portalQuotationKind: Record<string, string> = {
  INITIAL: 'Báo giá',
  ADDITIONAL: 'Báo giá phát sinh',
}
