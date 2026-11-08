import { FileText } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { PortalQuotationList } from './PortalQuotationList'
import { usePortalQuotations } from './usePortal'

export default function PortalQuotationsPage() {
  const { data: quotations, isLoading } = usePortalQuotations()

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-foreground">Báo giá</h1>
        <p className="mt-1 text-sm text-muted-foreground">Các báo giá designer đã gửi cho bạn.</p>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 2 }).map((_, i) => (
            <Skeleton key={i} className="h-24 w-full rounded-xl" />
          ))}
        </div>
      ) : !quotations || quotations.length === 0 ? (
        <div className="rounded-xl border bg-card shadow-card">
          <EmptyState
            icon={FileText}
            title="Chưa có báo giá nào"
            description="Khi designer gửi báo giá, báo giá sẽ hiện ở đây kèm mã QR chuyển khoản."
          />
        </div>
      ) : (
        <PortalQuotationList items={quotations} showProject />
      )}
    </div>
  )
}
