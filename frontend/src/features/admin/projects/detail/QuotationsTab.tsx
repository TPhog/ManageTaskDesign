import { useState } from 'react'
import { Link } from 'react-router-dom'
import { FileText, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'
import type { ProjectDetail } from '@/lib/types'
import { CreateQuotationDialog } from '../../quotations/CreateQuotationDialog'
import { groupChains, kindLabel } from '../../quotations/quotationUtils'
import { useProjectQuotations } from '../../quotations/useQuotations'

export function QuotationsTab({ project }: { project: ProjectDetail }) {
  const [createOpen, setCreateOpen] = useState(false)
  const { data, isLoading } = useProjectQuotations(project.id)
  const chains = groupChains(data?.items ?? [])

  const createButton = (
    <Button size="sm" onClick={() => setCreateOpen(true)}>
      <Plus aria-hidden="true" />
      Tạo báo giá
    </Button>
  )

  return (
    <div className="rounded-xl border bg-card shadow-card">
      <div className="flex items-center justify-between gap-3 border-b p-4">
        <p className="text-sm text-muted-foreground">{chains.length} báo giá</p>
        {createButton}
      </div>

      {isLoading ? (
        <div className="space-y-2 p-4">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : chains.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="Chưa có báo giá"
          description="Lập báo giá từ hạng mục của dự án — toàn bộ, hoặc chỉ các hạng mục đã xong."
          action={createButton}
        />
      ) : (
        <div className="relative overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className={tableHeadRowClass}>
                <th className="px-4 py-2.5">Số báo giá</th>
                <th className="px-4 py-2.5">Tiêu đề</th>
                <th className="px-4 py-2.5">Loại</th>
                <th className="px-4 py-2.5 text-right">Tổng</th>
                <th className="px-4 py-2.5">Trạng thái</th>
                <th className="px-4 py-2.5">Thanh toán</th>
                <th className="px-4 py-2.5">Hết hạn</th>
              </tr>
            </thead>
            <tbody>
              {chains.map(({ key, latest, versions }) => (
                <tr key={key} className={tableRowClass}>
                  <td className="whitespace-nowrap px-4 py-3 font-medium">
                    <Link to={`/admin/quotations/${latest.id}`} className="link-hover font-semibold text-foreground">
                      {latest.number ? `${latest.number} · v${latest.version}` : 'Bản nháp'}
                    </Link>
                    {versions.length > 1 && (
                      <span className="ml-2 text-xs text-muted-foreground">{versions.length} phiên bản</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-foreground">{latest.title}</td>
                  <td className="px-4 py-3 text-muted-foreground">{kindLabel[latest.kind]}</td>
                  <td className="px-4 py-3 text-right"><MoneyText amount={latest.total} /></td>
                  <td className="px-4 py-3"><StatusBadge status={latest.effectiveStatus} /></td>
                  <td className="px-4 py-3">
                    {latest.status === 'ACCEPTED'
                      ? <StatusBadge status={latest.paymentStatus} />
                      : <span className="text-muted-foreground">—</span>}
                  </td>
                  <td className="px-4 py-3 tabular-nums text-muted-foreground">{formatDate(latest.expiryDate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <CreateQuotationDialog project={project} open={createOpen} onClose={() => setCreateOpen(false)} />
    </div>
  )
}
