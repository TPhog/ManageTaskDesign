import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { api, emptyToNull } from '@/lib/api'
import type { ClientListItem } from '@/lib/types'

const schema = z.object({
  clientId: z.string().min(1, 'Chọn khách hàng'),
  name: z.string().min(1, 'Nhập tên dự án').max(200),
  designType: z.enum(['LOGO', 'BANNER', 'POSTER', 'BRANDING', 'UI_UX', 'SOCIAL_MEDIA', 'PACKAGING', 'PRINT', 'OTHER']),
  startDate: z.string().optional(),
  dueDate: z.string().optional(),
}).refine(v => !v.startDate || !v.dueDate || v.dueDate >= v.startDate, {
  message: 'Deadline phải sau ngày bắt đầu',
  path: ['dueDate'],
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

  const { data: clientsData, isLoading: loadingClients } = useQuery({
    queryKey: ['clients', 'active'],
    queryFn: () => api.get<{ items: ClientListItem[] }>('/admin/clients?status=ACTIVE&pageSize=100'),
    enabled: open,
  })
  const clients = clientsData?.items ?? []

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { clientId: '', designType: 'OTHER' },
  })

  const mutation = useMutation({
    // date inputs yield '' when left blank; the API expects null for "no date"
    mutationFn: (body: FormValues) => api.post('/admin/projects', emptyToNull(body)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects'] })
      toast.success('Đã tạo dự án')
      reset()
      onClose()
    },
    onError: () => toast.error('Không thể tạo dự án. Vui lòng thử lại.'),
  })

  const fieldError = (id: string, message?: string) =>
    message ? <p id={id} className="text-sm text-destructive">{message}</p> : null

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Tạo dự án mới</DialogTitle>
          <DialogDescription>Dự án mới bắt đầu ở trạng thái Nháp.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-5 py-2" noValidate>
          <div className="space-y-2">
            <Label htmlFor="project-client">Khách hàng <span className="text-destructive">*</span></Label>
            <Select value={watch('clientId')} onValueChange={v => setValue('clientId', v, { shouldValidate: true })}>
              <SelectTrigger
                id="project-client"
                aria-invalid={!!errors.clientId}
                aria-describedby={errors.clientId ? 'project-client-error' : undefined}
              >
                <SelectValue placeholder={loadingClients ? 'Đang tải…' : 'Chọn khách hàng'} />
              </SelectTrigger>
              <SelectContent>
                {clients.map(c => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}{c.company ? ` — ${c.company}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {!loadingClients && clients.length === 0 && (
              <p className="text-sm text-muted-foreground">
                Chưa có khách đang hoạt động.{' '}
                <Link to="/admin/clients" onClick={onClose} className="font-medium text-primary hover:underline">
                  Thêm khách trước
                </Link>
              </p>
            )}
            {fieldError('project-client-error', errors.clientId?.message)}
          </div>

          <div className="space-y-2">
            <Label htmlFor="project-name">Tên dự án <span className="text-destructive">*</span></Label>
            <Input
              id="project-name"
              placeholder="Vd: Thiết kế logo ABC"
              aria-invalid={!!errors.name}
              aria-describedby={errors.name ? 'project-name-error' : undefined}
              {...register('name')}
            />
            {fieldError('project-name-error', errors.name?.message)}
          </div>

          <div className="space-y-2">
            <Label htmlFor="project-type">Loại thiết kế <span className="text-destructive">*</span></Label>
            <Select
              value={watch('designType')}
              onValueChange={v => setValue('designType', v as FormValues['designType'])}
            >
              <SelectTrigger id="project-type">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {designTypeOptions.map(o => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="project-start">Ngày bắt đầu</Label>
              <Input id="project-start" type="date" {...register('startDate')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="project-due">Deadline</Label>
              <Input
                id="project-due"
                type="date"
                aria-invalid={!!errors.dueDate}
                aria-describedby={errors.dueDate ? 'project-due-error' : undefined}
                {...register('dueDate')}
              />
              {fieldError('project-due-error', errors.dueDate?.message)}
            </div>
          </div>

          <DialogFooter className="gap-2 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Đang tạo…' : 'Tạo dự án'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
