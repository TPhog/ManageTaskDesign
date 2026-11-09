import { Link } from 'react-router-dom'
import { MessageSquarePlus, MessagesSquare } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { Kicker } from './kit/editorial'
import { PortalRequestList } from './PortalRequestList'
import { usePortalRequests } from './usePortal'

export default function PortalRequestsPage() {
  const { data: requests, isLoading } = usePortalRequests()

  return (
    <div className="space-y-6">
      <header>
        <Kicker tone="highlight">Yêu cầu</Kicker>
        <h1 className="mt-2 font-display text-[2rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">Yêu cầu của tôi</h1>
        <p className="mt-1.5 font-serif text-base text-muted-foreground">Các yêu cầu chỉnh sửa bạn đã gửi và trạng thái xử lý.</p>
        <Button asChild className="mt-4 h-11 w-full sm:w-auto">
          <Link to="/portal/requests/new">
            <MessageSquarePlus aria-hidden="true" />
            Gửi yêu cầu
          </Link>
        </Button>
      </header>

      <div className="border-t-[3px] border-rule">
        {isLoading ? (
          <Skeleton className="mt-3 h-32 w-full" />
        ) : !requests || requests.length === 0 ? (
          <EmptyState
            icon={MessagesSquare}
            title="Chưa có yêu cầu nào"
            description="Khi cần chỉnh sửa, hãy gửi yêu cầu để nhà thiết kế tiếp nhận."
          />
        ) : (
          <PortalRequestList items={requests} showProject />
        )}
      </div>
    </div>
  )
}
