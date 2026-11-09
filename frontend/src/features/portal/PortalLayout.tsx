import { useState } from 'react'
import { Link, Outlet, useLocation } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { FileText, FolderOpen, Link2Off, Loader2, LogOut, MessagesSquare, WifiOff, type LucideIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { cn } from '@/lib/utils'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PortalMe } from '@/lib/portalTypes'
import { PortalMessage } from './PortalMessage'
import { PublicStudioContact, StudioContact } from './StudioContact'
import { isSessionExpired, logoutCompleted } from './errors'
import { usePortalTheme } from './kit/usePortalTheme'
import { Masthead } from './kit/Masthead'
import { EditorialSection } from './kit/editorial'

// docs/08 §3.1: bottom navigation of the portal (mobile-first)
const navItems: { to: string; label: string; icon: LucideIcon; match: (path: string) => boolean }[] = [
  { to: '/portal', label: 'Dự án', icon: FolderOpen, match: p => p === '/portal' || p.startsWith('/portal/projects') },
  { to: '/portal/quotations', label: 'Báo giá', icon: FileText, match: p => p.startsWith('/portal/quotations') },
  { to: '/portal/requests', label: 'Yêu cầu', icon: MessagesSquare, match: p => p.startsWith('/portal/requests') },
]

/** QA C-14: one wording for "you are out" — open the old link again, or ask the studio for a new one. */
const reopenHint = 'Mở lại link nhà thiết kế đã gửi cho bạn. Không tìm thấy link hoặc link không mở được? Nhắn nhà thiết kế để nhận link mới.'

export default function PortalLayout() {
  usePortalTheme()
  const qc = useQueryClient()
  const { pathname } = useLocation()
  const [loggedOut, setLoggedOut] = useState(false)
  const [confirmLogout, setConfirmLogout] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)

  const { data: me, isLoading, error, refetch } = useQuery({
    queryKey: qk.portal.me,
    queryFn: () => api.get<PortalMe>('/portal/me'),
    retry: false,
    enabled: !loggedOut,
  })

  async function logout() {
    setLoggingOut(true)
    try {
      await api.post('/portal/logout', {})
    } catch (err) {
      if (!logoutCompleted(err)) {
        toast.error('Không thoát được, vui lòng thử lại.')
        setLoggingOut(false)
        return
      }
    }
    qc.removeQueries({ queryKey: ['portal'] })
    setConfirmLogout(false)
    setLoggingOut(false)
    setLoggedOut(true)
  }

  if (loggedOut) {
    return <PortalMessage icon={LogOut} title="Bạn đã thoát" description={reopenHint} action={<PublicStudioContact />} />
  }

  if (isLoading) {
    return (
      <div role="status" className="flex min-h-screen items-center justify-center gap-2 bg-background text-sm text-muted-foreground">
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
    return <PortalMessage icon={Link2Off} title="Phiên xem đã hết hạn" description={reopenHint} action={<PublicStudioContact />} />
  }

  const { designer } = me
  const hasContact = !!(designer.contactPhone || designer.contactEmail)

  return (
    <div data-theme="editorial" className="min-h-screen bg-background text-foreground">
      <Masthead
        studioName={designer.displayName || 'Nhà thiết kế'}
        logoUrl={designer.logoUrl}
        homeHref="/portal"
        subline={`Xin chào, ${me.clientName}`}
        actions={
          <Button variant="ghost" onClick={() => setConfirmLogout(true)} className="h-11 gap-1.5 px-3">
            <LogOut aria-hidden="true" />
            Thoát
          </Button>
        }
      />
      <main className="mx-auto max-w-2xl px-5 pb-[calc(6rem+env(safe-area-inset-bottom))] pt-6">
        <Outlet />
        {hasContact && (
          <EditorialSection id="portal-contact" title="Cần hỏi nhà thiết kế?" className="mt-10">
            <p className="mt-1 text-sm text-muted-foreground">
              {designer.displayName || 'Nhà thiết kế'} trả lời qua điện thoại, Zalo hoặc email.
            </p>
            <StudioContact phone={designer.contactPhone} email={designer.contactEmail} className="mt-3" />
          </EditorialSection>
        )}
      </main>
      {/* CF-16: the link is the only way back in, so leaving asks first */}
      <ConfirmDialog
        open={confirmLogout}
        onOpenChange={setConfirmLogout}
        title="Thoát khỏi trang theo dõi?"
        description="Lần sau bạn mở lại bằng link nhà thiết kế đã gửi qua Zalo hoặc tin nhắn."
        confirmLabel="Thoát"
        pending={loggingOut}
        onConfirm={() => void logout()}
      />
      <nav
        aria-label="Điều hướng portal"
        className="fixed inset-x-0 bottom-0 z-10 border-t border-rule bg-background/95 pb-[env(safe-area-inset-bottom)] backdrop-blur"
      >
        <ul className="mx-auto flex max-w-2xl">
          {navItems.map(item => {
            const active = item.match(pathname)
            return (
              <li key={item.to} className="flex-1">
                <Link
                  to={item.to}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'flex h-14 flex-col items-center justify-center gap-0.5 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
                    active ? 'text-highlight-text shadow-[inset_0_2px_0_var(--highlight)]' : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  <item.icon className="h-5 w-5" aria-hidden="true" />
                  {item.label}
                </Link>
              </li>
            )
          })}
        </ul>
      </nav>
    </div>
  )
}
