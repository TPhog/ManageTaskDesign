import { Link } from 'react-router-dom'
import { Compass } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'
import { EmptyState } from '@/components/EmptyState'

/** Catch-all inside /portal: stays in the portal shell and never links to /admin. */
export default function PortalNotFound() {
  return (
    <EmptyState
      icon={Compass}
      title="Không tìm thấy trang"
      description="Trang bạn tìm không tồn tại hoặc đã được di chuyển."
      action={<Link to="/portal" className={buttonVariants()}>Về danh sách dự án</Link>}
    />
  )
}
