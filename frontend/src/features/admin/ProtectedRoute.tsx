import { Navigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { data: user, isLoading, error } = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ email: string }>('/auth/me'),
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen text-gray-500 text-sm">
        Đang tải...
      </div>
    )
  }

  if (error || !user) return <Navigate to="/admin/login" replace />

  return <>{children}</>
}
