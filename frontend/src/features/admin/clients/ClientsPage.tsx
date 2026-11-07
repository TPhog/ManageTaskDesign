import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ExternalLink } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { api } from '@/lib/api'
import type { PagedResult, ClientListItem } from '@/lib/types'

const clientSchema = z.object({
  name: z.string().min(1, 'Bắt buộc'),
  company: z.string().optional(),
  phone: z.string().optional(),
  email: z.string().email('Email không hợp lệ').optional().or(z.literal('')),
  contactChannel: z.string().optional(),
  notes: z.string().optional(),
})
type ClientForm = z.infer<typeof clientSchema>

function StatusChip({ status }: { status: 'ACTIVE' | 'INACTIVE' }) {
  return status === 'ACTIVE' ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-700">
      Đang hoạt động
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-500">
      Lưu trữ
    </span>
  )
}

export default function ClientsPage() {
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [sheetOpen, setSheetOpen] = useState(false)

  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (status) params.set('status', status)
  params.set('pageSize', '100')

  const { data, isLoading } = useQuery({
    queryKey: ['clients', q, status],
    queryFn: () => api.get<PagedResult<ClientListItem & { portalEnabled: boolean }>>(`/api/admin/clients?${params}`),
  })

  const { register, handleSubmit, formState: { errors }, reset } = useForm<ClientForm>({
    resolver: zodResolver(clientSchema),
  })

  const createMutation = useMutation({
    mutationFn: (body: ClientForm) => api.post('/api/admin/clients', body),
    onSuccess: () => {
      toast.success('Đã tạo khách hàng')
      qc.invalidateQueries({ queryKey: ['clients'] })
      setSheetOpen(false)
      reset()
    },
    onError: () => toast.error('Lỗi khi tạo khách hàng'),
  })

  const clients = data?.items ?? []

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-gray-900">Khách hàng</h1>
        <Button size="sm" onClick={() => setSheetOpen(true)}>
          <Plus className="h-4 w-4 mr-1.5" />
          Thêm khách
        </Button>
      </div>

      {/* toolbar */}
      <div className="flex items-center gap-3 flex-wrap">
        <Input
          placeholder="Tìm theo tên, công ty, SĐT…"
          value={q}
          onChange={e => setQ(e.target.value)}
          className="max-w-xs"
        />
        <Select value={status || '_all'} onValueChange={v => setStatus(v === '_all' ? '' : v)}>
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all">Tất cả</SelectItem>
            <SelectItem value="ACTIVE">Đang hoạt động</SelectItem>
            <SelectItem value="INACTIVE">Lưu trữ</SelectItem>
          </SelectContent>
        </Select>
        <span className="text-sm text-gray-400 ml-auto">{data?.total ?? 0} khách</span>
      </div>

      {/* table */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {isLoading ? (
          <div className="p-4 space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : clients.length === 0 ? (
          <div className="p-12 text-center text-sm text-gray-400">
            {q || status ? 'Không tìm thấy khách phù hợp' : 'Chưa có khách hàng nào'}
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wider">Tên</th>
                <th className="text-left px-4 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wider hidden md:table-cell">Công ty</th>
                <th className="text-left px-4 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wider hidden lg:table-cell">SĐT</th>
                <th className="text-left px-4 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wider">Trạng thái</th>
                <th className="text-right px-4 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wider">Dự án</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {clients.map(c => (
                <tr key={c.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3 font-medium text-gray-900">{c.name}</td>
                  <td className="px-4 py-3 text-gray-500 hidden md:table-cell">{c.company ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-500 hidden lg:table-cell">{c.phone ?? '—'}</td>
                  <td className="px-4 py-3">
                    <StatusChip status={c.status} />
                  </td>
                  <td className="px-4 py-3 text-right text-gray-500">{c.projectCount}</td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      to={`/admin/clients/${c.id}`}
                      className="inline-flex items-center gap-1 text-xs text-blue-600 hover:text-blue-800"
                    >
                      Chi tiết <ExternalLink className="h-3 w-3" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* create sheet */}
      <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
        <SheetContent>
          <SheetHeader>
            <SheetTitle>Thêm khách hàng</SheetTitle>
          </SheetHeader>
          <form
            onSubmit={handleSubmit(d => createMutation.mutate(d))}
            className="mt-4 space-y-4"
          >
            <div className="space-y-1.5">
              <Label htmlFor="name">Tên khách *</Label>
              <Input id="name" {...register('name')} />
              {errors.name && <p className="text-xs text-red-500">{errors.name.message}</p>}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="company">Công ty</Label>
              <Input id="company" {...register('company')} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="phone">Số điện thoại</Label>
                <Input id="phone" {...register('phone')} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" {...register('email')} />
                {errors.email && <p className="text-xs text-red-500">{errors.email.message}</p>}
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="contactChannel">Kênh liên hệ</Label>
              <Input id="contactChannel" placeholder="Zalo, Facebook…" {...register('contactChannel')} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="notes">Ghi chú nội bộ</Label>
              <Textarea id="notes" rows={3} {...register('notes')} />
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Đang lưu…' : 'Tạo khách'}
              </Button>
            </div>
          </form>
        </SheetContent>
      </Sheet>
    </div>
  )
}
