import { Navigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { api } from '@/lib/api'

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { data: user, isLoading, error } = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ username: string }>('/auth/me'),
    retry: false,
  })

  if (isLoading) {
    return (
      <div role="status" className="flex h-screen items-center justify-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        Đang tải…
      </div>
    )
  }

  if (error || !user) return <Navigate to="/admin/login" replace />

  return <>{children}</>
}
