import { useState } from 'react'
import { Link, Outlet } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Link2Off, Loader2, LogOut, Mail, Phone, WifiOff } from 'lucide-react'
import { Button, buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PortalMe } from '@/lib/portalTypes'
import { PortalMessage } from './PortalMessage'
import { isSessionExpired, logoutCompleted } from './errors'

export default function PortalLayout() {
  const qc = useQueryClient()
  const [loggedOut, setLoggedOut] = useState(false)

  const { data: me, isLoading, error, refetch } = useQuery({
    queryKey: qk.portal.me,
    queryFn: () => api.get<PortalMe>('/portal/me'),
    retry: false,
    enabled: !loggedOut,
  })

  async function logout() {
    try {
      await api.post('/portal/logout', {})
    } catch (err) {
      if (!logoutCompleted(err)) {
        toast.error('Không thoát được, vui lòng thử lại.')
        return
      }
    }
    qc.removeQueries({ queryKey: ['portal'] })
    setLoggedOut(true)
  }

  if (loggedOut) {
    return <PortalMessage icon={LogOut} title="Bạn đã thoát" description="Mở lại link designer đã gửi để xem tiếp dự án." />
  }

  if (isLoading) {
    return (
      <div role="status" className="flex min-h-screen items-center justify-center gap-2 bg-slate-50 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        Đang tải…
      </div>
    )
  }

  if (error && !isSessionExpired(error)) {
    return (
      <PortalMessage
        icon={WifiOff}
        title="Không tải được trang"
        description="Có lỗi khi tải dữ liệu. Vui lòng kiểm tra kết nối và thử lại."
        action={<Button onClick={() => void refetch()}>Thử lại</Button>}
      />
    )
  }

  if (error || !me) {
    return (
      <PortalMessage
        icon={Link2Off}
        title="Phiên xem đã hết hạn"
        description="Link có thể đã bị thu hồi hoặc thay bằng link mới. Vui lòng liên hệ designer để nhận link mới."
      />
    )
  }

  const { designer } = me
  const iconLink = buttonVariants({ variant: 'ghost', size: 'icon' })

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="sticky top-0 z-10 border-b bg-white/95 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-2xl items-center gap-1 px-4">
          <Link to="/portal" className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold text-foreground">{designer.displayName || 'Designer'}</p>
            <p className="truncate text-xs text-muted-foreground">Xin chào, {me.clientName}</p>
          </Link>
          {designer.contactPhone && (
            <a href={`tel:${designer.contactPhone}`} className={iconLink} aria-label="Gọi cho designer">
              <Phone aria-hidden="true" />
            </a>
          )}
          {designer.contactEmail && (
            <a href={`mailto:${designer.contactEmail}`} className={iconLink} aria-label="Gửi email cho designer">
              <Mail aria-hidden="true" />
            </a>
          )}
          <Button variant="ghost" size="icon" onClick={logout} aria-label="Thoát">
            <LogOut aria-hidden="true" />
          </Button>
        </div>
      </header>
      <main className="mx-auto max-w-2xl px-4 py-5">
        <Outlet />
      </main>
    </div>
  )
}
