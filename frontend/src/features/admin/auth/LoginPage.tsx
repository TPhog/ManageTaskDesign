import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { api } from '@/lib/api'
import { authGate } from '@/lib/authGate'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  username: z.string().min(1, 'Vui lòng nhập tên đăng nhập'),
  password: z.string().min(1, 'Vui lòng nhập mật khẩu'),
})
type FormValues = z.infer<typeof schema>

const highlights = [
  'Quản lý khách hàng, dự án và deliverable ở một nơi',
  'Theo dõi số vòng sửa theo hạn mức',
  'Báo giá kèm mã VietQR cho khách',
]

export default function LoginPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()

  const me = useQuery({
    queryKey: ['me'],
    queryFn: () => api.get<{ username: string }>('/auth/me'),
    retry: false,
  })
  const gate = authGate(me)
  const checkingAuth = gate === 'checking'

  // Redirect only on a successful check: after a failed refetch React Query still holds the old user
  useEffect(() => {
    if (gate === 'signed-in') navigate('/admin', { replace: true })
  }, [gate, navigate])

  const { register, handleSubmit, formState: { errors }, setError } = useForm<FormValues>({
    resolver: zodResolver(schema),
  })

  const loginMutation = useMutation({
    mutationFn: (data: FormValues) => api.post('/auth/login', data),
    onSuccess: (_res, vars) => {
      // replace the cached 401 so the gate does not bounce back to login once
      qc.setQueryData(['me'], { username: vars.username })
      navigate('/admin', { replace: true })
    },
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
    <div className="grid min-h-screen bg-background lg:grid-cols-2">
      {/* brand panel */}
      <div className="relative hidden overflow-hidden bg-primary p-12 text-primary-foreground lg:flex lg:flex-col lg:justify-between">
        <div
          className="pointer-events-none absolute -right-24 -top-24 h-96 w-96 rounded-full bg-white/10"
          aria-hidden="true"
        />
        <div
          className="pointer-events-none absolute -bottom-32 -left-16 h-80 w-80 rounded-full bg-white/5"
          aria-hidden="true"
        />
        <div className="relative flex items-center gap-2.5">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-white text-sm font-bold text-primary">
            FF
          </div>
          <span className="text-lg font-semibold">FreelanceFlow</span>
        </div>
        <div className="relative max-w-md">
          <p className="text-3xl font-semibold leading-tight">Không gian làm việc gọn gàng cho designer tự do.</p>
          <ul className="mt-8 space-y-3">
            {highlights.map(h => (
              <li key={h} className="flex items-start gap-3 text-sm text-white/90">
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                {h}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-xs text-white/70">© FreelanceFlow</p>
      </div>

      {/* form */}
      <div className="flex items-center justify-center px-6 py-12">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-2.5 lg:hidden">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground">
              FF
            </div>
            <span className="text-lg font-semibold">FreelanceFlow</span>
          </div>
          <h1 className="text-2xl font-semibold tracking-tight text-foreground">Đăng nhập</h1>
          <p className="mt-1 text-sm text-muted-foreground">Chào mừng trở lại. Nhập thông tin để tiếp tục.</p>

          <form onSubmit={handleSubmit(data => loginMutation.mutate(data))} className="mt-8 space-y-5" noValidate>
            <div className="space-y-2">
              <Label htmlFor="username">Tên đăng nhập</Label>
              <Input
                id="username"
                type="text"
                autoComplete="username"
                autoFocus
                aria-invalid={!!errors.username}
                aria-describedby={errors.username ? 'username-error' : undefined}
                {...register('username')}
              />
              {errors.username && (
                <p id="username-error" className="text-sm text-destructive">{errors.username.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Mật khẩu</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                aria-invalid={!!errors.password}
                aria-describedby={errors.password ? 'password-error' : undefined}
                {...register('password')}
              />
              {errors.password && (
                <p id="password-error" className="text-sm text-destructive">{errors.password.message}</p>
              )}
            </div>
            {errors.root && (
              <div role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700">
                {errors.root.message}
              </div>
            )}
            <Button type="submit" className="h-11 w-full" disabled={loginMutation.isPending}>
              {loginMutation.isPending && <Loader2 className="animate-spin" aria-hidden="true" />}
              {loginMutation.isPending ? 'Đang đăng nhập…' : 'Đăng nhập'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
