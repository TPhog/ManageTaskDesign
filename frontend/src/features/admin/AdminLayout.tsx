import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { LayoutDashboard, FolderKanban, Users, Briefcase, Settings, LogOut } from 'lucide-react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/admin', label: 'Tổng quan', icon: LayoutDashboard, end: true },
  { to: '/admin/projects', label: 'Dự án', icon: FolderKanban },
  { to: '/admin/clients', label: 'Khách hàng', icon: Users },
  { to: '/admin/services', label: 'Dịch vụ', icon: Briefcase },
  { to: '/admin/settings', label: 'Cài đặt', icon: Settings },
]

export default function AdminLayout() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const logoutMutation = useMutation({
    mutationFn: () => api.post('/api/auth/logout', {}),
    onSettled: () => {
      queryClient.clear()
      navigate('/admin/login', { replace: true })
    },
  })

  return (
    <div className="min-h-screen bg-gray-50">
      <aside className="fixed inset-y-0 left-0 w-56 bg-white border-r border-gray-200 flex flex-col">
        <div className="h-14 flex items-center px-4 border-b border-gray-200">
          <span className="font-semibold text-gray-900">FreelanceFlow</span>
        </div>
        <nav className="flex-1 py-4 px-2 space-y-0.5">
          {navItems.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-colors',
                  isActive
                    ? 'bg-blue-50 text-blue-700 font-medium'
                    : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
                )
              }
            >
              <item.icon className="h-4 w-4 shrink-0" />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="p-2 border-t border-gray-200">
          <Button
            variant="ghost"
            size="sm"
            className="w-full justify-start text-gray-500 hover:text-red-600"
            onClick={() => logoutMutation.mutate()}
            disabled={logoutMutation.isPending}
          >
            <LogOut className="h-4 w-4 mr-2" />
            Đăng xuất
          </Button>
        </div>
      </aside>
      <main className="ml-56 p-6">
        <Outlet />
      </main>
    </div>
  )
}
