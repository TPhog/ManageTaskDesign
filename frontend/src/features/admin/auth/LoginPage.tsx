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
  'Quản lý khách hàng, dự án và hạng mục ở một nơi',
  'Theo dõi số vòng sửa theo hạn mức',
  'Báo giá kèm mã VietQR cho khách',
]

/** Same mark as the admin sidebar: ink square, vermilion dot, paper corner. */
function BrandMark() {
  return (
    <div className="flex items-center gap-2.5">
      <span aria-hidden="true" className="relative h-[34px] w-[34px] shrink-0 rounded-[10px] bg-foreground">
        <span className="absolute right-1.5 top-1.5 h-3 w-3 rounded-full bg-highlight" />
        <span className="absolute bottom-1.5 left-1.5 h-3 w-3 rounded-[3px] bg-background" />
      </span>
      <span className="font-display text-[1.0625rem] font-bold tracking-[-0.02em] text-foreground">FreelanceFlow</span>
    </div>
  )
}

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
      <div className="tone-inverse relative hidden overflow-hidden p-12 lg:flex lg:flex-col lg:justify-between">
        <div className="pointer-events-none absolute right-12 top-12 h-24 w-24 rounded-full bg-highlight" aria-hidden="true" />
        <div className="relative">
          <BrandMark />
        </div>
        <div className="relative max-w-md">
          <p className="font-display text-display-lg font-bold">Không gian làm việc gọn gàng cho designer tự do.</p>
          <ul className="mt-8 space-y-3">
            {highlights.map(h => (
              <li key={h} className="flex items-start gap-3 text-sm text-on-inverse">
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-on-inverse-accent" aria-hidden="true" />
                {h}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-xs text-on-inverse-muted">© FreelanceFlow</p>
      </div>

      {/* form */}
      <div className="flex items-center justify-center px-6 py-12">
        <div className="w-full max-w-sm">
          <div className="mb-8 lg:hidden">
            <BrandMark />
          </div>
          <h1 className="font-display text-display-lg font-bold text-foreground">Đăng nhập</h1>
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
              <div role="alert" className="rounded-lg border border-danger/30 bg-danger-soft px-3 py-2.5 text-sm text-danger">
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
