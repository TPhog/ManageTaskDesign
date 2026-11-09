import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from './api'
import { PORTAL_INVITE_TEMPLATE, renderTemplate } from './messageTemplates'
import type { PortalLinkResult } from './types'

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

/**
 * Creates a fresh portal link for a client (POST /admin/clients/{id}/portal-link) and keeps the raw URL in memory:
 * the server stores only its hash, so this is the one moment the link can be copied. A new link retires the old one.
 */
export function usePortalLinkForClient(clientId: string | undefined) {
  const qc = useQueryClient()
  const [link, setLink] = useState<string | null>(null)
  const mutation = useMutation({
    mutationFn: () => api.post<PortalLinkResult>(`/admin/clients/${clientId}/portal-link`, {}),
    onSuccess: res => {
      setLink(res.url)
      void qc.invalidateQueries({ queryKey: ['clients'] })
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể tạo link portal'),
  })

  /** Resolves with the new URL, or null when creating failed (the error toast is already shown). */
  async function create(): Promise<string | null> {
    if (!clientId) return null
    try {
      return (await mutation.mutateAsync()).url
    } catch {
      return null
    }
  }

  return { link, create, clear: () => setLink(null), isPending: mutation.isPending }
}
