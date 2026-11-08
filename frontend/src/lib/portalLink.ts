/** Vietnamese message the designer pastes into Zalo together with the magic link. */
export function portalInviteMessage(clientName: string, url: string): string {
  return [
    `Chào ${clientName},`,
    `Mình gửi bạn link theo dõi tiến độ dự án: ${url}`,
    'Bạn mở link để xem trạng thái, các hạng mục và tiến trình công việc, không cần đăng nhập.',
    'Link dành riêng cho bạn, vui lòng không chuyển tiếp cho người khác nhé.',
  ].join('\n')
}

export type PortalLinkStatus = 'NONE' | 'ACTIVE' | 'DISABLED'

/** `badge` is a StatusBadge style key; `label` is the text shown. */
export function portalLinkState(c: { hasPortalLink: boolean; portalEnabled: boolean }): {
  status: PortalLinkStatus
  badge: string
  label: string
} {
  if (!c.hasPortalLink) return { status: 'NONE', badge: 'DRAFT', label: 'Chưa có link' }
  if (c.portalEnabled) return { status: 'ACTIVE', badge: 'ACTIVE', label: 'Đang hoạt động' }
  return { status: 'DISABLED', badge: 'ON_HOLD', label: 'Đã tắt' }
}
