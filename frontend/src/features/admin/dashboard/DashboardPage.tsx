import { Link } from 'react-router-dom'
import { FolderKanban, Users, Briefcase, ArrowRight } from 'lucide-react'
import { PageHeader } from '@/components/PageHeader'

const shortcuts = [
  { to: '/admin/projects', icon: FolderKanban, title: 'Dự án', desc: 'Theo dõi tiến độ, deadline và trạng thái' },
  { to: '/admin/clients', icon: Users, title: 'Khách hàng', desc: 'Quản lý thông tin liên hệ của khách' },
  { to: '/admin/services', icon: Briefcase, title: 'Dịch vụ', desc: 'Bảng giá và số vòng sửa mặc định' },
]

// Dashboard widgets are scheduled for M8; until then show quick navigation.
export default function DashboardPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Tổng quan" description="Bảng điều khiển chi tiết sẽ có ở bản cập nhật sau." />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {shortcuts.map(s => (
          <Link
            key={s.to}
            to={s.to}
            className="group rounded-xl border bg-card p-5 shadow-card transition-colors duration-150 hover:border-primary/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-secondary text-primary">
              <s.icon className="h-5 w-5" aria-hidden="true" />
            </div>
            <p className="mt-4 flex items-center gap-1 font-semibold text-foreground">
              {s.title}
              <ArrowRight className="h-4 w-4 opacity-0 transition-opacity group-hover:opacity-100" aria-hidden="true" />
            </p>
            <p className="mt-1 text-sm text-muted-foreground">{s.desc}</p>
          </Link>
        ))}
      </div>
    </div>
  )
}
