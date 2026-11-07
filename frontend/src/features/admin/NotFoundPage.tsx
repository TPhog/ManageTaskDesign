import { Link, isRouteErrorResponse, useRouteError } from 'react-router-dom'
import { Compass, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'

/** Used both as the catch-all route and as the router errorElement. */
export default function NotFoundPage() {
  const error = useRouteError()
  const isNotFound = !error || (isRouteErrorResponse(error) && error.status === 404)
  const Icon = isNotFound ? Compass : TriangleAlert

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-background px-6 text-center">
      <div className="mb-5 flex h-14 w-14 items-center justify-center rounded-full bg-secondary text-primary">
        <Icon className="h-7 w-7" aria-hidden="true" />
      </div>
      <p className="text-sm font-semibold text-primary">{isNotFound ? '404' : 'Lỗi'}</p>
      <h1 className="mt-1 text-2xl font-semibold tracking-tight text-foreground">
        {isNotFound ? 'Không tìm thấy trang' : 'Đã có lỗi xảy ra'}
      </h1>
      <p className="mt-2 max-w-sm text-sm text-muted-foreground">
        {isNotFound
          ? 'Trang bạn tìm không tồn tại hoặc đã được di chuyển.'
          : 'Vui lòng tải lại trang. Nếu lỗi lặp lại, hãy thử lại sau.'}
      </p>
      <Button asChild className="mt-6">
        <Link to="/admin">Về trang tổng quan</Link>
      </Button>
    </div>
  )
}
