import { useNavigate } from 'react-router-dom'
import { StatusBadge } from '@/components/StatusBadge'
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
}

export function ProjectsTable({ projects }: Props) {
  const navigate = useNavigate()
  const today = new Date().toISOString().slice(0, 10)

  if (projects.length === 0) {
    return (
      <div className="text-center py-16 text-gray-400 text-sm">
        Chưa có dự án nào
      </div>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100 text-left">
            <th className="pb-2 pr-4 font-medium text-gray-500 w-[260px]">Tên dự án</th>
            <th className="pb-2 pr-4 font-medium text-gray-500">Khách hàng</th>
            <th className="pb-2 pr-4 font-medium text-gray-500">Loại</th>
            <th className="pb-2 pr-4 font-medium text-gray-500">Trạng thái</th>
            <th className="pb-2 pr-4 font-medium text-gray-500">Bắt đầu</th>
            <th className="pb-2 pr-4 font-medium text-gray-500">Deadline</th>
            <th className="pb-2 font-medium text-gray-500 text-right">Deliverable</th>
          </tr>
        </thead>
        <tbody>
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
                className="border-b border-gray-50 hover:bg-gray-50 cursor-pointer transition-colors"
              >
                <td className="py-3 pr-4">
                  <span className="font-medium text-gray-900 line-clamp-1">{p.name}</span>
                </td>
                <td className="py-3 pr-4 text-gray-600">{p.client.name}</td>
                <td className="py-3 pr-4 text-gray-500">
                  {designTypeLabel[p.designType] ?? p.designType}
                </td>
                <td className="py-3 pr-4">
                  <StatusBadge status={p.status} />
                </td>
                <td className="py-3 pr-4 text-gray-500 tabular-nums">
                  {formatDate(p.startDate)}
                </td>
                <td className="py-3 pr-4">
                  <span className={`tabular-nums ${isOverdue ? 'text-red-600 font-medium' : 'text-gray-500'}`}>
                    {formatDate(p.dueDate)}
                  </span>
                </td>
                <td className="py-3 text-right text-gray-500">{p.deliverableCount}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
