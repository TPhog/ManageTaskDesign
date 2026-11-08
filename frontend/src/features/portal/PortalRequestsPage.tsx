import { Link } from 'react-router-dom'
import { MessageSquarePlus, MessagesSquare } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { PortalRequestList } from './PortalRequestList'
import { usePortalRequests } from './usePortal'

export default function PortalRequestsPage() {
  const { data: requests, isLoading } = usePortalRequests()

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-foreground">Yêu cầu của tôi</h1>
          <p className="mt-1 text-sm text-muted-foreground">Các yêu cầu chỉnh sửa bạn đã gửi và trạng thái xử lý.</p>
        </div>
        <Link to="/portal/requests/new" className={buttonVariants({ size: 'sm' })}>
          <MessageSquarePlus aria-hidden="true" />
          Gửi yêu cầu
        </Link>
      </div>

      {isLoading ? (
        <Skeleton className="h-32 w-full rounded-xl" />
      ) : !requests || requests.length === 0 ? (
        <div className="rounded-xl border bg-card shadow-card">
          <EmptyState
            icon={MessagesSquare}
            title="Chưa có yêu cầu nào"
            description="Khi cần chỉnh sửa, hãy gửi yêu cầu để designer tiếp nhận."
          />
        </div>
      ) : (
        <div className="rounded-xl border bg-card p-4 shadow-card sm:p-5">
          <PortalRequestList items={requests} showProject />
        </div>
      )}
    </div>
  )
}
