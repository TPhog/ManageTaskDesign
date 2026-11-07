import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

const schema = z.object({
  username: z.string().min(1, 'Vui lòng nhập tên đăng nhập'),
  password: z.string().min(1, 'Vui lòng nhập mật khẩu'),
})
type FormValues = z.infer<typeof schema>

export default function LoginPage() {
  const navigate = useNavigate()

  const { data: user, isLoading: checkingAuth } = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ username: string }>('/auth/me'),
    retry: false,
  })

  useEffect(() => {
    if (user) navigate('/admin', { replace: true })
  }, [user, navigate])

  const { register, handleSubmit, formState: { errors }, setError } = useForm<FormValues>({
    resolver: zodResolver(schema),
  })

  const loginMutation = useMutation({
    mutationFn: (data: FormValues) => api.post('/auth/login', data),
    onSuccess: () => navigate('/admin', { replace: true }),
    onError: (err: Error & { status?: number }) => {
      if (err.status === 429) {
        setError('root', { message: 'Đăng nhập quá nhiều lần. Vui lòng thử lại sau.' })
      } else {
        setError('root', { message: 'Tên đăng nhập hoặc mật khẩu không đúng' })
      }
    },
  })

  if (checkingAuth) return null

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center">
      <Card className="w-[400px]">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">FreelanceFlow</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(data => loginMutation.mutate(data))} className="space-y-4">
            <div className="space-y-1">
              <Label htmlFor="username">Tên đăng nhập</Label>
              <Input id="username" type="text" placeholder="designer" {...register('username')} />
              {errors.username && <p className="text-sm text-red-500">{errors.username.message}</p>}
            </div>
            <div className="space-y-1">
              <Label htmlFor="password">Mật khẩu</Label>
              <Input id="password" type="password" {...register('password')} />
              {errors.password && <p className="text-sm text-red-500">{errors.password.message}</p>}
            </div>
            {errors.root && (
              <p className="text-sm text-red-500 text-center">{errors.root.message}</p>
            )}
            <Button type="submit" className="w-full" disabled={loginMutation.isPending}>
              {loginMutation.isPending ? 'Đang đăng nhập...' : 'Đăng nhập'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
