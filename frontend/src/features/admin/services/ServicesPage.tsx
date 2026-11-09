import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Eye, EyeOff, Briefcase } from 'lucide-react'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { MoneyInput } from '@/components/MoneyInput'
import { MoneyText } from '@/components/MoneyText'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { EmptyState } from '@/components/EmptyState'
import { StatusBadge } from '@/components/StatusBadge'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'
import { TaskTemplateEditor } from './TaskTemplateEditor'
import { templateError } from './templateUtils'

// Mirrors ServiceDto in Features/Services/ServicesEndpoints.cs
interface Service {
  id: string
  name: string
  description: string | null
  basePrice: number
  defaultIncludedRounds: number
  extraRoundPrice: number
  active: boolean
  /** plan 06b: ordered step names used by "Bắt đầu dự án" */
  taskTemplate: string[]
}

const schema = z.object({
  name: z.string().min(1, 'Vui lòng nhập tên dịch vụ'),
  description: z.string().nullable(),
  basePrice: z.number().int().min(0, 'Không được âm'),
  defaultIncludedRounds: z.coerce.number().int().min(0, 'Không được âm').max(50, 'Tối đa 50'),
  extraRoundPrice: z.number().int().min(0, 'Không được âm'),
})
type FormValues = z.infer<typeof schema>

export default function ServicesPage() {
  const [showInactive, setShowInactive] = useState(false)
  const queryClient = useQueryClient()
  const [sheetOpen, setSheetOpen] = useState(false)
  const [editing, setEditing] = useState<Service | null>(null)
  const [template, setTemplate] = useState<string[]>([])
  const [templateErr, setTemplateErr] = useState<string | null>(null)

  const { data: services, isLoading } = useQuery({
    queryKey: ['services'],
    // API returns only active services by default; the page filters hidden ones client-side
    queryFn: () => api.get<Service[]>('/admin/services?includeInactive=true'),
  })

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { register, control, handleSubmit, reset, formState: { errors }, setError } = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
  })

  function openCreate() {
    setEditing(null)
    reset({ name: '', description: '', basePrice: 0, defaultIncludedRounds: 0, extraRoundPrice: 0 })
    setTemplate([])
    setTemplateErr(null)
    setSheetOpen(true)
  }

  function openEdit(svc: Service) {
    setEditing(svc)
    reset({
      name: svc.name,
      description: svc.description ?? '',
      basePrice: svc.basePrice,
      defaultIncludedRounds: svc.defaultIncludedRounds,
      extraRoundPrice: svc.extraRoundPrice,
    })
    setTemplate(svc.taskTemplate ?? [])
    setTemplateErr(null)
    setSheetOpen(true)
  }

  const saveMutation = useMutation({
    mutationFn: (data: FormValues) => {
      const body = { ...data, taskTemplate: template.map(s => s.trim()) }
      return editing
        ? api.put(`/admin/services/${editing.id}`, { ...body, active: editing.active })
        : api.post('/admin/services', body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['services'] })
      setSheetOpen(false)
      toast.success(editing ? 'Đã cập nhật dịch vụ' : 'Đã tạo dịch vụ')
    },
    onError: (err: ApiError) => {
      if (err.status === 409) {
        setError('name', { message: 'Đã có dịch vụ trùng tên' })
      } else if (err.errors?.taskTemplate) {
        setTemplateErr(err.errors.taskTemplate[0] ?? 'Bước mẫu không hợp lệ')
      } else {
        toast.error('Lưu thất bại. Vui lòng thử lại.')
      }
    },
  })

  // There is no delete/restore endpoint: hiding a service is an update of its `active` flag.
  const toggleMutation = useMutation({
    mutationFn: (svc: Service) =>
      api.put(`/admin/services/${svc.id}`, {
        name: svc.name,
        description: svc.description,
        basePrice: svc.basePrice,
        defaultIncludedRounds: svc.defaultIncludedRounds,
        extraRoundPrice: svc.extraRoundPrice,
        active: !svc.active,
      }),
    onSuccess: (_, svc) => {
      queryClient.invalidateQueries({ queryKey: ['services'] })
      toast.success(svc.active ? 'Đã ẩn dịch vụ' : 'Đã hiện lại dịch vụ')
    },
    onError: () => toast.error('Thất bại. Vui lòng thử lại.'),
  })

  const visible = services?.filter(s => showInactive || s.active) ?? []
  const hiddenCount = services?.filter(s => !s.active).length ?? 0

  const fieldA11y = (name: keyof FormValues) => ({
    'aria-invalid': !!errors[name],
    'aria-describedby': errors[name] ? `service-${name}-error` : undefined,
  })
  const fieldError = (name: keyof FormValues) =>
    errors[name] ? <p id={`service-${name}-error`} className="text-sm text-destructive">{errors[name]?.message}</p> : null

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dịch vụ"
        description="Bảng giá gốc và số vòng sửa miễn phí, dùng khi lập báo giá."
        actions={
          <Button onClick={openCreate}>
            <Plus aria-hidden="true" />
            Thêm dịch vụ
          </Button>
        }
      />

      <div className="rounded-xl border bg-card shadow-card">
        <div className="flex items-center justify-between gap-3 border-b p-4">
          <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
            <input
              type="checkbox"
              checked={showInactive}
              onChange={e => setShowInactive(e.target.checked)}
              className="h-4 w-4 rounded border-input accent-primary"
            />
            Hiện dịch vụ đã ẩn{hiddenCount > 0 ? ` (${hiddenCount})` : ''}
          </label>
          <span className="text-sm text-muted-foreground">{visible.length} dịch vụ</span>
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
          </div>
        ) : visible.length === 0 ? (
          <EmptyState
            icon={Briefcase}
            title="Chưa có dịch vụ nào"
            description="Tạo dịch vụ để dùng làm mẫu khi lập báo giá."
            action={
              <Button onClick={openCreate}>
                <Plus aria-hidden="true" />
                Thêm dịch vụ
              </Button>
            }
          />
        ) : (
          <div className="relative min-w-0 max-w-full overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className={tableHeadRowClass}>
                  <th scope="col" className="px-4 py-3">Tên</th>
                  <th scope="col" className="px-4 py-3 text-right">Giá cơ bản</th>
                  <th scope="col" className="hidden px-4 py-3 text-right sm:table-cell">Vòng sửa</th>
                  <th scope="col" className="hidden px-4 py-3 text-right md:table-cell">Giá vòng sửa thêm</th>
                  <th scope="col" className="hidden px-4 py-3 md:table-cell">Trạng thái</th>
                  <th scope="col" className="px-4 py-3"><span className="sr-only">Thao tác</span></th>
                </tr>
              </thead>
              <tbody>
                {visible.map(svc => (
                  <tr key={svc.id} className={`${tableRowClass} ${svc.active ? '' : 'opacity-60'}`}>
                    <td className="px-4 py-3.5">
                      <div className="font-semibold text-foreground">{svc.name}</div>
                      {svc.description && <div className="max-w-xs truncate text-xs text-muted-foreground">{svc.description}</div>}
                      {(svc.taskTemplate?.length ?? 0) > 0 && (
                        <div className="text-xs text-muted-foreground">{svc.taskTemplate.length} bước mẫu</div>
                      )}
                    </td>
                    <td className="px-4 py-3.5 text-right font-medium tabular-nums"><MoneyText amount={svc.basePrice} /></td>
                    <td className="hidden px-4 py-3.5 text-right tabular-nums text-muted-foreground sm:table-cell">{svc.defaultIncludedRounds}</td>
                    <td className="hidden px-4 py-3.5 text-right tabular-nums text-muted-foreground md:table-cell"><MoneyText amount={svc.extraRoundPrice} /></td>
                    <td className="hidden px-4 py-3.5 md:table-cell">
                      <StatusBadge status={svc.active ? 'ACTIVE' : 'INACTIVE'} label={svc.active ? 'Đang dùng' : 'Đã ẩn'} />
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <Button variant="ghost" size="icon" aria-label={`Sửa ${svc.name}`} onClick={() => openEdit(svc)}>
                          <Pencil aria-hidden="true" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={svc.active ? `Ẩn ${svc.name}` : `Hiện ${svc.name}`}
                          title={svc.active ? 'Ẩn' : 'Hiện'}
                          onClick={() => toggleMutation.mutate(svc)}
                          disabled={toggleMutation.isPending}
                        >
                          {svc.active ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
        <SheetContent className="flex w-full flex-col sm:max-w-md">
          <SheetHeader>
            <SheetTitle>{editing ? 'Sửa dịch vụ' : 'Thêm dịch vụ'}</SheetTitle>
            <SheetDescription>Giá tính bằng VND, không có phần thập phân.</SheetDescription>
          </SheetHeader>
          <form
            onSubmit={handleSubmit(data => {
              const error = templateError(template)
              setTemplateErr(error)
              if (!error) saveMutation.mutate(data)
            })}
            className="mt-6 flex min-h-0 flex-1 flex-col"
            noValidate
          >
            <div className="flex-1 space-y-5 overflow-y-auto pr-1">
              <div className="space-y-2">
                <Label htmlFor="service-name">Tên dịch vụ</Label>
                <Input id="service-name" {...fieldA11y('name')} {...register('name')} />
                {fieldError('name')}
              </div>
              <div className="space-y-2">
                <Label htmlFor="service-description">Mô tả</Label>
                <Textarea id="service-description" rows={3} {...register('description')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="service-basePrice">Giá gốc (₫)</Label>
                <Controller
                  name="basePrice"
                  control={control}
                  render={({ field }) => (
                    <MoneyInput id="service-basePrice" value={field.value ?? 0} onChange={field.onChange} onBlur={field.onBlur} ref={field.ref} {...fieldA11y('basePrice')} />
                  )}
                />
                {fieldError('basePrice')}
              </div>
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="service-defaultIncludedRounds">Số vòng sửa miễn phí</Label>
                  <Input id="service-defaultIncludedRounds" type="number" min={0} max={50} {...fieldA11y('defaultIncludedRounds')} {...register('defaultIncludedRounds')} />
                  {fieldError('defaultIncludedRounds')}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="service-extraRoundPrice">Giá vòng sửa thêm (₫)</Label>
                  <Controller
                    name="extraRoundPrice"
                    control={control}
                    render={({ field }) => (
                      <MoneyInput id="service-extraRoundPrice" value={field.value ?? 0} onChange={field.onChange} onBlur={field.onBlur} ref={field.ref} {...fieldA11y('extraRoundPrice')} />
                    )}
                  />
                  {fieldError('extraRoundPrice')}
                </div>
              </div>
              <TaskTemplateEditor value={template} onChange={setTemplate} error={templateErr} />
            </div>
            <SheetFooter className="mt-6 gap-2">
              <Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>Huỷ</Button>
              <Button type="submit" disabled={saveMutation.isPending}>
                {saveMutation.isPending ? 'Đang lưu…' : 'Lưu'}
              </Button>
            </SheetFooter>
          </form>
        </SheetContent>
      </Sheet>
    </div>
  )
}
