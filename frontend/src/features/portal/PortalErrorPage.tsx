import { isRouteErrorResponse, Link, useRouteError } from 'react-router-dom'
import { Compass, TriangleAlert } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'
import { PortalMessage } from './PortalMessage'

/** errorElement for the portal routes: portal styling, no /admin links. */
export default function PortalErrorPage() {
  const error = useRouteError()
  const notFound = isRouteErrorResponse(error) && error.status === 404
  return (
    <PortalMessage
      icon={notFound ? Compass : TriangleAlert}
      title={notFound ? 'Không tìm thấy trang' : 'Đã có lỗi xảy ra'}
      description={notFound ? 'Trang bạn tìm không tồn tại hoặc đã được di chuyển.' : 'Vui lòng tải lại trang. Nếu lỗi lặp lại, hãy thử lại sau.'}
      action={<Link to="/portal" className={buttonVariants()}>Về danh sách dự án</Link>}
    />
  )
}
