import { PORTAL_INVITE_TEMPLATE, renderTemplate } from './messageTemplates'

/** Vietnamese message the designer pastes into Zalo together with the magic link. */
export function portalInviteMessage(clientName: string, url: string): string {
  return renderTemplate(PORTAL_INVITE_TEMPLATE, { 'tên': clientName, 'link portal': url })
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
