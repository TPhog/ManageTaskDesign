import { Link, useNavigate } from 'react-router-dom'
import { FolderKanban, Plus, SearchX } from 'lucide-react'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { Button } from '@/components/ui/button'
import { formatDate } from '@/lib/format'
import type { ProjectListItem } from '@/lib/types'

const designTypeLabel: Record<string, string> = {
  LOGO: 'Logo',
  BANNER: 'Banner',
  POSTER: 'Poster',
  BRANDING: 'Thương hiệu',
  UI_UX: 'UI/UX',
  SOCIAL_MEDIA: 'Mạng xã hội',
  PACKAGING: 'Bao bì',
  PRINT: 'In ấn',
  OTHER: 'Khác',
}

interface Props {
  projects: ProjectListItem[]
  filtering: boolean
  onCreate: () => void
}

export function ProjectsTable({ projects, filtering, onCreate }: Props) {
  const navigate = useNavigate()
  const today = new Date().toISOString().slice(0, 10)

  if (projects.length === 0) {
    return filtering ? (
      <EmptyState icon={SearchX} title="Không tìm thấy dự án phù hợp" description="Thử từ khoá khác hoặc bỏ bộ lọc." />
    ) : (
      <EmptyState
        icon={FolderKanban}
        title="Chưa có dự án nào"
        description="Tạo dự án đầu tiên cho khách của bạn."
        action={
          <Button onClick={onCreate}>
            <Plus aria-hidden="true" />
            Tạo dự án
          </Button>
        }
      />
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="whitespace-nowrap border-b bg-muted/50 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">
            <th scope="col" className="px-4 py-3">Tên dự án</th>
            <th scope="col" className="hidden px-4 py-3 md:table-cell">Khách hàng</th>
            <th scope="col" className="hidden px-4 py-3 lg:table-cell">Loại</th>
            <th scope="col" className="px-4 py-3">Trạng thái</th>
            <th scope="col" className="hidden px-4 py-3 lg:table-cell">Bắt đầu</th>
            <th scope="col" className="px-4 py-3">Deadline</th>
            <th scope="col" className="hidden px-4 py-3 text-right sm:table-cell">Hạng mục</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {projects.map(p => {
            const isOverdue =
              p.dueDate &&
              p.dueDate < today &&
              p.status !== 'COMPLETED' &&
              p.status !== 'CANCELLED'

            return (
              <tr
                key={p.id}
                onClick={() => navigate(`/admin/projects/${p.id}`)}
                className="cursor-pointer transition-colors duration-150 hover:bg-muted/40"
              >
                <td className="px-4 py-3.5">
                  {/* real link so the row is reachable by keyboard and opens in a new tab */}
                  <Link
                    to={`/admin/projects/${p.id}`}
                    onClick={e => e.stopPropagation()}
                    className="line-clamp-1 font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm"
                  >
                    {p.name}
                  </Link>
                  <span className="text-xs text-muted-foreground md:hidden">{p.client.name}</span>
                </td>
                <td className="hidden px-4 py-3.5 text-muted-foreground md:table-cell">{p.client.name}</td>
                <td className="hidden px-4 py-3.5 text-muted-foreground lg:table-cell">
                  {designTypeLabel[p.designType] ?? p.designType}
                </td>
                <td className="px-4 py-3.5">
                  <StatusBadge status={p.status} />
                </td>
                <td className="hidden px-4 py-3.5 tabular-nums text-muted-foreground lg:table-cell">
                  {formatDate(p.startDate)}
                </td>
                <td className="px-4 py-3.5">
                  <span className={`tabular-nums ${isOverdue ? 'font-medium text-red-700' : 'text-muted-foreground'}`}>
                    {formatDate(p.dueDate)}
                    {isOverdue && <span className="sr-only"> (quá hạn)</span>}
                  </span>
                </td>
                <td className="hidden px-4 py-3.5 text-right tabular-nums text-muted-foreground sm:table-cell">{p.deliverableCount}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
