import { Bell, Building2, FileText, Images, KeyRound, Landmark, Users, type LucideIcon } from 'lucide-react'

export const SETTINGS_TABS = [
  { key: 'studio', label: 'Studio', icon: Building2 },
  { key: 'payment', label: 'Thanh toán', icon: Landmark },
  { key: 'quotation', label: 'Báo giá', icon: FileText },
  { key: 'portal', label: 'Khách hàng & Portal', icon: Users },
  { key: 'images', label: 'Ảnh & Dung lượng', icon: Images },
  { key: 'notifications', label: 'Thông báo', icon: Bell },
  { key: 'account', label: 'Tài khoản', icon: KeyRound },
] as const satisfies readonly { key: string; label: string; icon: LucideIcon }[]

export type SettingsTab = (typeof SETTINGS_TABS)[number]['key']

/** `?tab=` → a known tab; anything else (missing, typo, old link) opens Studio. */
export function parseSettingsTab(value: string | null): SettingsTab {
  return SETTINGS_TABS.find(t => t.key === value)?.key ?? 'studio'
}
