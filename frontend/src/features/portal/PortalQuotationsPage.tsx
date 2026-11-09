import { FileText } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { Kicker } from './kit/editorial'
import { PortalQuotationList } from './PortalQuotationList'
import { usePortalQuotations } from './usePortal'

export default function PortalQuotationsPage() {
  const { data: quotations, isLoading } = usePortalQuotations()

  return (
    <div className="space-y-6">
      <header>
        <Kicker tone="highlight">Báo giá</Kicker>
        <h1 className="mt-2 font-display text-[2rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">Báo giá</h1>
        <p className="mt-1.5 font-serif text-base text-muted-foreground">Các báo giá nhà thiết kế đã gửi cho bạn.</p>
      </header>

      <div className="border-t-[3px] border-rule">
        {isLoading ? (
          <div className="mt-3 space-y-3">
            {Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-20 w-full" />)}
          </div>
        ) : !quotations || quotations.length === 0 ? (
          <EmptyState
            icon={FileText}
            title="Chưa có báo giá nào"
            description="Khi nhà thiết kế gửi báo giá, báo giá sẽ hiện ở đây kèm mã QR chuyển khoản."
          />
        ) : (
          <PortalQuotationList items={quotations} showProject />
        )}
      </div>
    </div>
  )
}
