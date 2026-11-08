import { Navigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, ServerCrash } from 'lucide-react'
import { api } from '@/lib/api'
import { authGate } from '@/lib/authGate'
import { Button } from '@/components/ui/button'

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const me = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ username: string }>('/auth/me'),
    retry: false,
  })
  const gate = authGate(me)

  if (gate === 'checking') {
    return (
      <div role="status" className="flex h-screen items-center justify-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        Đang tải…
      </div>
    )
  }

  if (gate === 'unavailable') {
    return (
      <div role="alert" className="flex h-screen flex-col items-center justify-center gap-3 px-6 text-center">
        <ServerCrash className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
        <p className="font-medium text-foreground">Không kết nối được máy chủ</p>
        <p className="text-sm text-muted-foreground">Vui lòng kiểm tra kết nối rồi thử lại.</p>
        <Button variant="outline" onClick={() => me.refetch()} disabled={me.isFetching}>
          {me.isFetching && <Loader2 className="animate-spin" aria-hidden="true" />}
          Thử lại
        </Button>
      </div>
    )
  }

  if (gate === 'signed-out') return <Navigate to="/admin/login" replace />

  return <>{children}</>
}
