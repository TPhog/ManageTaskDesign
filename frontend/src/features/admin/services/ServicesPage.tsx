import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil } from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { MoneyText } from '@/components/MoneyText'
import { Skeleton } from '@/components/ui/skeleton'

interface Service {
  id: string
  name: string
  description: string | null
  basePrice: number
  freeRevisionRounds: number
  extraRoundPrice: number
  isActive: boolean
}

const schema = z.object({
  name: z.string().min(1, 'Bắt buộc'),
  description: z.string().nullable(),
  basePrice: z.coerce.number().int().min(0, 'Không âm'),
  freeRevisionRounds: z.coerce.number().int().min(0).max(50),
  extraRoundPrice: z.coerce.number().int().min(0, 'Không âm'),
})
type FormValues = z.infer<typeof schema>

export default function ServicesPage() {
  const queryClient = useQueryClient()
  const [showInactive, setShowInactive] = useState(false)
  const [sheetOpen, setSheetOpen] = useState(false)
  const [editing, setEditing] = useState<Service | null>(null)

  const { data: services, isLoading } = useQuery({
    queryKey: ['services'],
    queryFn: () => api.get<Service[]>('/admin/services'),
  })

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { register, handleSubmit, reset, formState: { errors }, setError } = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
  })

  function openCreate() {
    setEditing(null)
    reset({ name: '', description: '', basePrice: 0, freeRevisionRounds: 0, extraRoundPrice: 0 })
    setSheetOpen(true)
  }

  function openEdit(svc: Service) {
    setEditing(svc)
    reset({
      name: svc.name,
      description: svc.description ?? '',
      basePrice: svc.basePrice,
      freeRevisionRounds: svc.freeRevisionRounds,
      extraRoundPrice: svc.extraRoundPrice,
    })
    setSheetOpen(true)
  }

  const saveMutation = useMutation({
    mutationFn: (data: FormValues) =>
      editing
        ? api.put(`/admin/services/${editing.id}`, data)
        : api.post('/admin/services', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['services'] })
      setSheetOpen(false)
      toast.success(editing ? 'Đã cập nhật dịch vụ' : 'Đã tạo dịch vụ')
    },
    onError: (err: Error & { status?: number }) => {
      if (err.status === 409) {
        setError('name', { message: 'Đã có dịch vụ trùng tên' })
      } else {
        toast.error('Lưu thất bại. Vui lòng thử lại.')
      }
    },
  })

  const toggleMutation = useMutation({
    mutationFn: (svc: Service) =>
      svc.isActive
        ? api.delete(`/admin/services/${svc.id}`)
        : api.post(`/admin/services/${svc.id}/restore`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['services'] })
      toast.success('Đã cập nhật trạng thái')
    },
    onError: () => toast.error('Thất bại. Vui lòng thử lại.'),
  })

  const visible = services?.filter(s => showInactive || s.isActive) ?? []

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Dịch vụ</h1>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
            <input
              type="checkbox"
              checked={showInactive}
              onChange={e => setShowInactive(e.target.checked)}
              className="rounded"
            />
            Hiện dịch vụ ẩn
          </label>
          <Button size="sm" onClick={openCreate}>
            <Plus className="h-4 w-4 mr-1" />
            Thêm dịch vụ
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
        </div>
      ) : (
        <div className="rounded-md border bg-white overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-600">Tên</th>
                <th className="text-right px-4 py-3 font-medium text-gray-600">Giá cơ bản</th>
                <th className="text-right px-4 py-3 font-medium text-gray-600">Vòng sửa</th>
                <th className="text-right px-4 py-3 font-medium text-gray-600">Giá/vòng vượt</th>
                <th className="text-center px-4 py-3 font-medium text-gray-600">Trạng thái</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {visible.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">Chưa có dịch vụ nào</td>
                </tr>
              ) : (
                visible.map(svc => (
                  <tr key={svc.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="font-medium">{svc.name}</div>
                      {svc.description && <div className="text-xs text-gray-400 truncate max-w-xs">{svc.description}</div>}
                    </td>
                    <td className="px-4 py-3 text-right"><MoneyText amount={svc.basePrice} /></td>
                    <td className="px-4 py-3 text-right">{svc.freeRevisionRounds}</td>
                    <td className="px-4 py-3 text-right"><MoneyText amount={svc.extraRoundPrice} /></td>
                    <td className="px-4 py-3 text-center">
                      <Badge variant={svc.isActive ? 'default' : 'secondary'}>
                        {svc.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(svc)}>
                          <Pencil className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-gray-500"
                          onClick={() => toggleMutation.mutate(svc)}
                          disabled={toggleMutation.isPending}
                        >
                          {svc.isActive ? 'Ẩn' : 'Hiện'}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
        <SheetContent>
          <SheetHeader>
            <SheetTitle>{editing ? 'Sửa dịch vụ' : 'Thêm dịch vụ'}</SheetTitle>
          </SheetHeader>
          <form onSubmit={handleSubmit(data => saveMutation.mutate(data))} className="mt-6 space-y-4 flex flex-col h-[calc(100%-4rem)]">
            <div className="flex-1 space-y-4 overflow-y-auto pr-1">
              <div className="space-y-1">
                <Label>Tên dịch vụ</Label>
                <Input {...register('name')} />
                {errors.name && <p className="text-sm text-red-500">{errors.name.message}</p>}
              </div>
              <div className="space-y-1">
                <Label>Mô tả</Label>
                <Textarea rows={3} {...register('description')} />
              </div>
              <div className="space-y-1">
                <Label>Giá cơ bản (₫)</Label>
                <Input type="number" min={0} step={1000} {...register('basePrice')} />
                {errors.basePrice && <p className="text-sm text-red-500">{errors.basePrice.message}</p>}
              </div>
              <div className="space-y-1">
                <Label>Số vòng sửa miễn phí</Label>
                <Input type="number" min={0} max={50} {...register('freeRevisionRounds')} />
                {errors.freeRevisionRounds && <p className="text-sm text-red-500">{errors.freeRevisionRounds.message}</p>}
              </div>
              <div className="space-y-1">
                <Label>Giá mỗi vòng vượt (₫)</Label>
                <Input type="number" min={0} step={1000} {...register('extraRoundPrice')} />
                {errors.extraRoundPrice && <p className="text-sm text-red-500">{errors.extraRoundPrice.message}</p>}
              </div>
            </div>
            <SheetFooter>
              <Button type="submit" className="w-full" disabled={saveMutation.isPending}>
                {saveMutation.isPending ? 'Đang lưu...' : 'Lưu'}
              </Button>
            </SheetFooter>
          </form>
        </SheetContent>
      </Sheet>
    </div>
  )
}
