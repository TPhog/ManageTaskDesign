import { useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LayoutDashboard, FolderKanban, ListChecks, Users, Briefcase, Settings, LogOut, Menu } from 'lucide-react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/admin', label: 'Tổng quan', icon: LayoutDashboard, end: true },
  { to: '/admin/projects', label: 'Dự án', icon: FolderKanban },
  { to: '/admin/tasks', label: 'Công việc', icon: ListChecks },
  { to: '/admin/clients', label: 'Khách hàng', icon: Users },
  { to: '/admin/services', label: 'Dịch vụ', icon: Briefcase },
  { to: '/admin/settings', label: 'Cài đặt', icon: Settings },
]

function BrandMark() {
  return (
    <div className="flex items-center gap-2.5">
      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground">
        FF
      </div>
      <span className="font-semibold tracking-tight text-foreground">FreelanceFlow</span>
    </div>
  )
}

function SidebarContent({ onNavigate, onLogout, loggingOut, username }: {
  onNavigate?: () => void
  onLogout: () => void
  loggingOut: boolean
  username?: string
}) {
  return (
    <div className="flex h-full flex-col">
      <div className="flex h-16 items-center px-5">
        <BrandMark />
      </div>
      <nav aria-label="Điều hướng chính" className="flex-1 space-y-1 px-3 py-2">
        {navItems.map(item => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors duration-150',
                isActive
                  ? 'bg-secondary text-secondary-foreground'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              )
            }
          >
            <item.icon className="h-[18px] w-[18px] shrink-0" aria-hidden="true" />
            {item.label}
          </NavLink>
        ))}
      </nav>
      <div className="border-t p-3">
        {username && (
          <div className="mb-2 flex items-center gap-3 px-3 py-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted text-xs font-semibold uppercase text-muted-foreground">
              {username.slice(0, 2)}
            </div>
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-foreground">{username}</p>
              <p className="text-xs text-muted-foreground">Designer</p>
            </div>
          </div>
        )}
        <Button
          variant="ghost"
          className="w-full justify-start text-muted-foreground hover:bg-red-50 hover:text-red-700"
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
    loggingOut: logoutMutation.isPending,
    username: me?.username,
  }

  return (
    <div className="min-h-screen bg-background">
      {/* desktop sidebar */}
      <aside className="fixed inset-y-0 left-0 hidden w-64 border-r bg-card lg:block">
        <SidebarContent {...sidebarProps} />
      </aside>

      {/* mobile top bar */}
      <header className="sticky top-0 z-40 flex h-14 items-center gap-3 border-b bg-card/95 px-4 backdrop-blur lg:hidden">
        <Button
          variant="ghost"
          size="icon"
          className="h-11 w-11"
          aria-label="Mở menu"
          onClick={() => setMobileOpen(true)}
        >
          <Menu className="h-5 w-5" />
        </Button>
        <span className="font-semibold text-foreground">{current?.label ?? 'FreelanceFlow'}</span>
      </header>

      <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
        <SheetContent side="left" className="w-72 p-0">
          <SheetTitle className="sr-only">Menu</SheetTitle>
          <SidebarContent {...sidebarProps} onNavigate={() => setMobileOpen(false)} />
        </SheetContent>
      </Sheet>

      <main className="lg:pl-64">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
