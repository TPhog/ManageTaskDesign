// Plan 12 glossary — xem plan-12 'Bảng thuật ngữ'. Client-facing wording (docs/08 §3.3): no internal terms such as
// "deliverable", "round", "hạng mục", "lần sửa", "brief" or "designer".

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
  IN_PROGRESS: 'Đang làm',
  IN_REVIEW: 'Chờ bạn duyệt',
  APPROVED: 'Đã duyệt',
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
  SENT: 'Chờ bạn đồng ý',
  ACCEPTED: 'Đã chốt',
  REJECTED: 'Không tiếp tục',
  EXPIRED: 'Đã hết hạn',
  SUPERSEDED: 'Phiên bản cũ',
}

export const portalPaymentStatus: Record<string, string> = {
  UNPAID: 'Chưa thanh toán',
  DEPOSIT_PAID: 'Đã nhận cọc',
  PAID: 'Đã thanh toán',
}

export const portalRequestStatus: Record<string, string> = {
  NEW: 'Đã gửi — chờ nhà thiết kế xem',
  TRIAGED: 'Nhà thiết kế đã nhận, đang xử lý',
  DECLINED: 'Nhà thiết kế không nhận yêu cầu này',
}

export const portalQuotationKind: Record<string, string> = {
  INITIAL: 'Báo giá',
  ADDITIONAL: 'Báo giá phát sinh',
}

export const portalBilling: Record<string, string> = {
  FREE_FIX: 'Miễn phí (lỗi của nhà thiết kế)',
  IN_QUOTA: 'Dùng lượt miễn phí',
  CHARGEABLE: 'Tính phí',
}

export const portalWorkStatus: Record<string, string> = {
  TODO: 'Chưa bắt đầu',
  IN_PROGRESS: 'Đang sửa',
  DONE: 'Đã sửa xong',
  CANCELLED: 'Đã huỷ',
}
