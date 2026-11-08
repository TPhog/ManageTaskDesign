import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link2, Plus, Search, Users, SearchX } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { EmptyState } from '@/components/EmptyState'
import { StatusBadge } from '@/components/StatusBadge'
import { api, emptyToNull } from '@/lib/api'
import type { PagedResult, ClientListItem } from '@/lib/types'
import { PortalLinkDialog } from './PortalLinkDialog'

const clientSchema = z.object({
  name: z.string().min(1, 'Vui lòng nhập tên khách'),
  company: z.string().optional(),
  phone: z.string().optional(),
  email: z.string().email('Email không hợp lệ').optional().or(z.literal('')),
  contactChannel: z.string().optional(),
  notes: z.string().optional(),
})
type ClientForm = z.infer<typeof clientSchema>

export default function ClientsPage() {
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [sheetOpen, setSheetOpen] = useState(false)
  const [portalClient, setPortalClient] = useState<{ id: string; name: string } | null>(null)

  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (status) params.set('status', status)
  params.set('pageSize', '100')

  const { data, isLoading } = useQuery({
    queryKey: ['clients', q, status],
    queryFn: () => api.get<PagedResult<ClientListItem & { portalEnabled: boolean }>>(`/admin/clients?${params}`),
  })

  const { register, handleSubmit, formState: { errors }, reset } = useForm<ClientForm>({
    resolver: zodResolver(clientSchema),
  })

  const createMutation = useMutation({
    mutationFn: (body: ClientForm) => api.post('/admin/clients', emptyToNull(body)),
    onSuccess: () => {
      toast.success('Đã tạo khách hàng')
      qc.invalidateQueries({ queryKey: ['clients'] })
      setSheetOpen(false)
      reset()
    },
    onError: () => toast.error('Không thể tạo khách hàng. Vui lòng thử lại.'),
  })

  const clients = data?.items ?? []
  const filtering = !!(q || status)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Khách hàng"
        description="Danh sách khách và số dự án của từng người."
        actions={
          <Button onClick={() => setSheetOpen(true)}>
            <Plus aria-hidden="true" />
            Thêm khách
          </Button>
        }
      />

      <div className="rounded-xl border bg-card shadow-card">
        {/* toolbar */}
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center">
          <div className="relative w-full sm:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              aria-label="Tìm khách hàng"
              placeholder="Tìm theo tên, công ty, SĐT…"
              value={q}
              onChange={e => setQ(e.target.value)}
              className="pl-9"
            />
          </div>
          <Select value={status || '_all'} onValueChange={v => setStatus(v === '_all' ? '' : v)}>
            <SelectTrigger className="w-full sm:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="_all">Tất cả trạng thái</SelectItem>
              <SelectItem value="ACTIVE">Đang hoạt động</SelectItem>
              <SelectItem value="INACTIVE">Lưu trữ</SelectItem>
            </SelectContent>
          </Select>
          <span className="text-sm text-muted-foreground sm:ml-auto" aria-live="polite">
            {data?.total ?? 0} khách
          </span>
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-11 w-full" />
            ))}
          </div>
        ) : clients.length === 0 ? (
          filtering ? (
            <EmptyState icon={SearchX} title="Không tìm thấy khách phù hợp" description="Thử từ khoá khác hoặc bỏ bộ lọc." />
          ) : (
            <EmptyState
              icon={Users}
              title="Chưa có khách hàng nào"
              description="Thêm khách đầu tiên để bắt đầu tạo dự án."
              action={
                <Button onClick={() => setSheetOpen(true)}>
                  <Plus aria-hidden="true" />
                  Thêm khách
                </Button>
              }
            />
          )
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="whitespace-nowrap border-b bg-muted/50 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  <th scope="col" className="px-4 py-3">Tên</th>
                  <th scope="col" className="hidden px-4 py-3 md:table-cell">Công ty</th>
                  <th scope="col" className="hidden px-4 py-3 lg:table-cell">SĐT</th>
                  <th scope="col" className="px-4 py-3">Trạng thái</th>
                  <th scope="col" className="px-4 py-3 text-right">Dự án</th>
                  <th scope="col" className="px-4 py-3 text-right">Portal</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {clients.map(c => (
                  <tr key={c.id} className="transition-colors duration-150 hover:bg-muted/40">
                    <td className="px-4 py-3.5">
                      <div className="flex items-center gap-3">
                        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-secondary text-xs font-semibold uppercase text-secondary-foreground" aria-hidden="true">
                          {c.name.slice(0, 1)}
                        </div>
                        <span className="font-medium text-foreground">{c.name}</span>
                      </div>
                    </td>
                    <td className="hidden px-4 py-3.5 text-muted-foreground md:table-cell">{c.company ?? '—'}</td>
                    <td className="hidden px-4 py-3.5 tabular-nums text-muted-foreground lg:table-cell">{c.phone ?? '—'}</td>
                    <td className="px-4 py-3.5">
                      <StatusBadge status={c.status} />
                    </td>
                    <td className="px-4 py-3.5 text-right tabular-nums text-muted-foreground">{c.projectCount}</td>
                    <td className="px-2 py-2 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={`Link portal của ${c.name}`}
                        onClick={() => setPortalClient({ id: c.id, name: c.name })}
                      >
                        <Link2 aria-hidden="true" />
                        <span className={c.portalEnabled ? 'text-emerald-700' : 'text-muted-foreground'}>
                          {c.portalEnabled ? 'Đang bật' : 'Chưa bật'}
                        </span>
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* create sheet */}
      <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Thêm khách hàng</SheetTitle>
            <SheetDescription>Chỉ tên là bắt buộc, các thông tin khác có thể bổ sung sau.</SheetDescription>
          </SheetHeader>
          <form onSubmit={handleSubmit(d => createMutation.mutate(d))} className="mt-6 space-y-5" noValidate>
            <div className="space-y-2">
              <Label htmlFor="name">Tên khách <span className="text-destructive">*</span></Label>
              <Input
                id="name"
                aria-invalid={!!errors.name}
                aria-describedby={errors.name ? 'name-error' : undefined}
                {...register('name')}
              />
              {errors.name && <p id="name-error" className="text-sm text-destructive">{errors.name.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="company">Công ty</Label>
              <Input id="company" {...register('company')} />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="phone">Số điện thoại</Label>
                <Input id="phone" type="tel" inputMode="tel" {...register('phone')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  aria-invalid={!!errors.email}
                  aria-describedby={errors.email ? 'email-error' : undefined}
                  {...register('email')}
                />
                {errors.email && <p id="email-error" className="text-sm text-destructive">{errors.email.message}</p>}
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="contactChannel">Kênh liên hệ</Label>
              <Input id="contactChannel" placeholder="Zalo, Facebook…" {...register('contactChannel')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="notes">Ghi chú nội bộ</Label>
              <Textarea id="notes" rows={3} {...register('notes')} />
              <p className="text-xs text-muted-foreground">Khách không nhìn thấy ghi chú này.</p>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>
                Huỷ
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Đang lưu…' : 'Tạo khách'}
              </Button>
            </div>
          </form>
        </SheetContent>
      </Sheet>

      <PortalLinkDialog client={portalClient} onClose={() => setPortalClient(null)} />
    </div>
  )
}
