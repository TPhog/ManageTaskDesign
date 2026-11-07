import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { api } from '@/lib/api'
import type { ClientListItem } from '@/lib/types'

const schema = z.object({
  clientId: z.string().min(1, 'Chọn khách hàng'),
  name: z.string().min(1, 'Nhập tên dự án').max(200),
  designType: z.enum(['LOGO', 'BANNER', 'POSTER', 'BRANDING', 'UI_UX', 'SOCIAL_MEDIA', 'PACKAGING', 'PRINT', 'OTHER']),
  startDate: z.string().optional(),
  dueDate: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

const designTypeOptions = [
  { value: 'LOGO', label: 'Logo' },
  { value: 'BANNER', label: 'Banner' },
  { value: 'POSTER', label: 'Poster' },
  { value: 'BRANDING', label: 'Thương hiệu' },
  { value: 'UI_UX', label: 'UI/UX' },
  { value: 'SOCIAL_MEDIA', label: 'Mạng xã hội' },
  { value: 'PACKAGING', label: 'Bao bì' },
  { value: 'PRINT', label: 'In ấn' },
  { value: 'OTHER', label: 'Khác' },
]

interface Props {
  open: boolean
  onClose: () => void
}

export function CreateProjectDialog({ open, onClose }: Props) {
  const qc = useQueryClient()

  const { data: clientsData } = useQuery({
    queryKey: ['clients', 'active'],
    queryFn: () => api.get<{ items: ClientListItem[] }>('/api/admin/clients?status=ACTIVE&pageSize=100'),
    enabled: open,
  })
  const clients = clientsData?.items ?? []

  const {
    register,
    handleSubmit,
    setValue,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { designType: 'OTHER' },
  })

  const mutation = useMutation({
    mutationFn: (body: FormValues) => api.post('/api/admin/projects', body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects'] })
      toast.success('Đã tạo dự án')
      reset()
      onClose()
    },
    onError: () => toast.error('Không thể tạo dự án'),
  })

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Tạo dự án mới</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label>Khách hàng *</Label>
            <Select onValueChange={v => setValue('clientId', v)} defaultValue="">
              <SelectTrigger>
                <SelectValue placeholder="Chọn khách hàng" />
              </SelectTrigger>
              <SelectContent>
                {clients.map(c => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}{c.company ? ` — ${c.company}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.clientId && <p className="text-xs text-red-500">{errors.clientId.message}</p>}
          </div>

          <div className="space-y-1.5">
            <Label>Tên dự án *</Label>
            <Input {...register('name')} placeholder="Vd: Thiết kế logo ABC" />
            {errors.name && <p className="text-xs text-red-500">{errors.name.message}</p>}
          </div>

          <div className="space-y-1.5">
            <Label>Loại thiết kế *</Label>
            <Select
              onValueChange={v => setValue('designType', v as FormValues['designType'])}
              defaultValue="OTHER"
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {designTypeOptions.map(o => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Ngày bắt đầu</Label>
              <Input type="date" {...register('startDate')} />
            </div>
            <div className="space-y-1.5">
              <Label>Deadline</Label>
              <Input type="date" {...register('dueDate')} />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>Huỷ</Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Đang tạo…' : 'Tạo dự án'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
