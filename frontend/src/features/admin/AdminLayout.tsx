import { Fragment, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LayoutDashboard, FolderKanban, ListChecks, FileText, Users, Briefcase, Settings, LogOut, Menu } from 'lucide-react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet'
import { cn } from '@/lib/utils'
import { Inbox, Search, UserPlus } from 'lucide-react'
import { useNewIntakeCount } from '@/features/admin/intake/useIntake'
import { useNewRequestCount } from '@/features/admin/revisions/useRevisions'
import { QuickSearch, QuickSearchTrigger } from '@/features/admin/search/QuickSearch'
import { StorageBanner } from './settings/StorageBanner'

const navItems: { to: string; label: string; icon: typeof LayoutDashboard; end?: boolean; group?: 'studio' }[] = [
  { to: '/admin', label: 'Tổng quan', icon: LayoutDashboard, end: true },
  { to: '/admin/requests', label: 'Yêu cầu', icon: Inbox },
  { to: '/admin/intake', label: 'Khách mới', icon: UserPlus },
  { to: '/admin/projects', label: 'Dự án', icon: FolderKanban },
  { to: '/admin/tasks', label: 'Công việc', icon: ListChecks },
  { to: '/admin/quotations', label: 'Báo giá', icon: FileText },
  { to: '/admin/clients', label: 'Khách hàng', icon: Users },
  { to: '/admin/services', label: 'Dịch vụ', icon: Briefcase, group: 'studio' as const },
  { to: '/admin/settings', label: 'Cài đặt', icon: Settings, group: 'studio' as const },
]

function BrandMark() {
  return (
    <div className="flex items-center gap-2.5">
      <span aria-hidden="true" className="relative h-[34px] w-[34px] shrink-0 rounded-[10px] bg-foreground">
        <span className="absolute right-1.5 top-1.5 h-3 w-3 rounded-full bg-highlight" />
        <span className="absolute bottom-1.5 left-1.5 h-3 w-3 rounded-[3px] bg-background" />
      </span>
      <span className="font-display text-[1.0625rem] font-bold tracking-[-0.02em] text-foreground">FreelanceFlow</span>
    </div>
  )
}

/** REQ-03: number of NEW requests next to "Yêu cầu" */
function NewRequestsBadge() {
  const { data } = useNewRequestCount()
  if (!data?.count) return null
  return (
    <span
      className="ml-auto rounded-full bg-highlight px-2 py-0.5 text-xs font-semibold tabular-nums text-highlight-foreground"
      aria-label={`${data.count} yêu cầu mới`}
    >
      {data.count}
    </span>
  )
}

/** Plan 13: number of NEW public-form submissions next to "Khách mới" */
function NewIntakeBadge() {
  const { data } = useNewIntakeCount()
  if (!data?.count) return null
  return (
    <span
      className="ml-auto rounded-full bg-highlight px-2 py-0.5 text-xs font-semibold tabular-nums text-highlight-foreground"
      aria-label={`${data.count} khách mới`}
    >
      {data.count}
    </span>
  )
}

function SidebarContent({ onNavigate, onLogout, onOpenSearch, loggingOut, username }: {
  onNavigate?: () => void
  onLogout: () => void
  onOpenSearch: () => void
  loggingOut: boolean
  username?: string
}) {
  return (
    <div className="flex h-full flex-col">
      <div className="px-5 pb-2 pt-6">
        <BrandMark />
      </div>
      <div className="px-3 pb-2">
        <QuickSearchTrigger onClick={onOpenSearch} />
      </div>
      <nav aria-label="Điều hướng chính" className="grid flex-1 content-start gap-0.5 overflow-y-auto px-3 py-2">
        {navItems.map((item, i) => (
          <Fragment key={item.to}>
            {item.group === 'studio' && navItems[i - 1]?.group !== 'studio' && (
              <p className="kicker px-3 pb-1.5 pt-4 text-muted-foreground">Studio</p>
            )}
            <NavLink
              to={item.to}
              end={item.end}
              onClick={onNavigate}
              className={({ isActive }) =>
                cn(
                  'flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isActive ? 'bg-primary text-primary-foreground' : 'text-foreground hover:bg-surface-hover',
                )
              }
            >
              <item.icon className="h-[18px] w-[18px] shrink-0" aria-hidden="true" />
              {item.label}
              {item.to === '/admin/requests' && <NewRequestsBadge />}
              {item.to === '/admin/intake' && <NewIntakeBadge />}
            </NavLink>
          </Fragment>
        ))}
      </nav>
      {username && (
        <div className="mx-3 mb-1 flex items-center gap-3 rounded-lg border bg-card p-3">
          <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-full bg-highlight text-xs font-bold uppercase text-highlight-foreground">
            {username.slice(0, 2)}
          </div>
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-foreground">{username}</p>
            <p className="text-xs text-muted-foreground">Designer</p>
          </div>
        </div>
      )}
      <div className="px-3 pb-3">
        <Button
          variant="ghost"
          className="w-full justify-start text-muted-foreground hover:bg-danger-soft hover:text-danger"
          onClick={onLogout}
          disabled={loggingOut}
        >
          <LogOut className="h-4 w-4" aria-hidden="true" />
          Đăng xuất
        </Button>
      </div>
    </div>
  )
}

export default function AdminLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)

  const { data: me } = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ username: string }>('/auth/me'),
    retry: false,
  })

  const logoutMutation = useMutation({
    mutationFn: () => api.post('/auth/logout', {}),
    onSettled: () => {
      queryClient.clear()
      navigate('/admin/login', { replace: true })
    },
  })

  const current = navItems.find(i => (i.end ? location.pathname === i.to : location.pathname.startsWith(i.to)))

  const sidebarProps = {
    onLogout: () => logoutMutation.mutate(),
    onOpenSearch: () => setSearchOpen(true),
    loggingOut: logoutMutation.isPending,
    username: me?.username,
  }

  return (
    <div className="min-h-screen bg-background">
      {/* desktop sidebar */}
      <aside className="fixed inset-y-0 left-0 hidden w-60 border-r bg-background lg:block">
        <SidebarContent {...sidebarProps} />
      </aside>

      {/* mobile top bar */}
      <header className="sticky top-0 z-40 flex h-14 items-center gap-3 border-b bg-background/95 px-4 backdrop-blur lg:hidden">
        <Button
          variant="ghost"
          size="icon"
          className="h-11 w-11"
          aria-label="Mở menu"
          onClick={() => setMobileOpen(true)}
        >
          <Menu className="h-5 w-5" />
        </Button>
        <span className="font-display font-bold text-foreground">{current?.label ?? 'FreelanceFlow'}</span>
        <Button
          variant="ghost"
          size="icon"
          className="ml-auto h-11 w-11"
          aria-label="Tìm kiếm nhanh"
          onClick={() => setSearchOpen(true)}
        >
          <Search className="h-5 w-5" />
        </Button>
      </header>

      <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
        <SheetContent side="left" className="w-72 p-0">
          <SheetTitle className="sr-only">Menu</SheetTitle>
          <SidebarContent
            {...sidebarProps}
            onNavigate={() => setMobileOpen(false)}
            onOpenSearch={() => {
              setMobileOpen(false)
              setSearchOpen(true)
            }}
          />
        </SheetContent>
      </Sheet>

      <main className="lg:pl-60">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <StorageBanner />
          <Outlet />
        </div>
      </main>

      <QuickSearch open={searchOpen} onOpenChange={setSearchOpen} />
    </div>
  )
}
