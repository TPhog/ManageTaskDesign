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
