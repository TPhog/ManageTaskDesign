import { useCallback, useEffect, useRef, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { MoneyInput } from '@/components/MoneyInput'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateInput } from '@/components/DateInput'
import { SearchCombobox } from '@/components/SearchCombobox'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { Deliverable, ExternalLink } from '@/lib/types'
import { LinkListEditor } from '@/features/admin/tasks/LinkListEditor'
import { useReportDirty } from '@/features/admin/tasks/useDirtyMap'

// Radix Select reserves '' for "no value"
const NO_SERVICE = '_none'

/** Case- and accent-insensitive match for the local service search ("thiet ke" finds "Thiết kế Logo"). */
const fold = (s: string) =>
  s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase()

const schema = z.object({
  name: z.string().min(1, 'Vui lòng nhập tên').max(200),
  quantity: z.coerce.number().int().min(1, 'Tối thiểu 1'),
  serviceId: z.string(),
  includedRounds: z.coerce.number().int().min(0, 'Không được âm').max(50, 'Tối đa 50'),
  extraRoundPrice: z.number().int().min(0, 'Không được âm'),
  dueDate: z.string().optional(),
})
type FormValues = z.infer<typeof schema>

const CREATE_DEFAULTS: FormValues = {
  name: '',
  quantity: 1,
  serviceId: NO_SERVICE,
  includedRounds: 0,
  extraRoundPrice: 0,
  dueDate: '',
}

interface ServiceOption {
  id: string
  name: string
  defaultIncludedRounds: number
  extraRoundPrice: number
}

interface Props {
  projectId: string
  open: boolean
  onClose: () => void
  /** null = create */
  editing: Deliverable | null
}

export function DeliverableDialog({ projectId, open, onClose, editing }: Props) {
  const qc = useQueryClient()
  // The links block saves on its own: closing the dialog with unsaved link edits asks first
  const linksDirty = useRef(false)
  const reportLinksDirty = useCallback((dirty: boolean) => { linksDirty.current = dirty }, [])
  const [confirmClose, setConfirmClose] = useState(false)
  const requestClose = () => (linksDirty.current ? setConfirmClose(true) : onClose())

  const { data: services = [] } = useQuery({
    queryKey: ['services', 'active'],
    queryFn: () => api.get<ServiceOption[]>('/admin/services'),
    enabled: open && !editing,
  })

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { register, control, handleSubmit, reset, setValue, getValues, watch, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: CREATE_DEFAULTS,
  })

  useEffect(() => {
    if (!open) return
    reset(editing
      ? {
          name: editing.name,
          quantity: editing.quantity,
          serviceId: editing.serviceId ?? NO_SERVICE,
          includedRounds: editing.includedRounds,
          extraRoundPrice: editing.extraRoundPrice,
          dueDate: editing.dueDate ?? '',
        }
      : CREATE_DEFAULTS)
  }, [open, editing, reset])

  // Picking a service pre-fills the quota fields from its defaults (docs/05 §3.6)
  function onServiceChange(id: string) {
    setValue('serviceId', id)
    const svc = services.find(s => s.id === id)
    if (!svc) return
    setValue('includedRounds', svc.defaultIncludedRounds)
    setValue('extraRoundPrice', svc.extraRoundPrice)
    if (!getValues('name')) setValue('name', svc.name)
  }

  const [serviceSearch, setServiceSearch] = useState('')
  const serviceOptions = services
    .filter(s => fold(s.name).includes(fold(serviceSearch.trim())))
    .map(s => ({ value: s.id, label: s.name }))
  const serviceId = watch('serviceId')

  const mutation = useMutation({
    mutationFn: (v: FormValues) => {
      const dueDate = v.dueDate || null
      return editing
        ? api.put<{ warnings: string[] }>(`/admin/deliverables/${editing.id}`, {
            name: v.name,
            quantity: v.quantity,
            includedRounds: v.includedRounds,
            extraRoundPrice: v.extraRoundPrice,
            dueDate,
            sortOrder: editing.sortOrder,
          })
        : api.post<unknown>(`/admin/projects/${projectId}/deliverables`, {
            name: v.name,
            quantity: v.quantity,
            serviceId: v.serviceId === NO_SERVICE ? null : v.serviceId,
            includedRounds: v.includedRounds,
            extraRoundPrice: v.extraRoundPrice,
            dueDate,
          })
    },
    onSuccess: res => {
      // create changes the list's deliverableCount; edit only affects this project
      qc.invalidateQueries({ queryKey: editing ? ['projects', projectId] : ['projects'] })
      qc.invalidateQueries({ queryKey: ['revisions'] })
      // the deliverable's due date is a calendar marker
      qc.invalidateQueries({ queryKey: qk.tasks.calendarAll })
      // BR-44: lowering the quota below used rounds is allowed but the API warns
      for (const w of (res as { warnings?: string[] } | undefined)?.warnings ?? []) toast.warning(w)
      toast.success(editing ? 'Đã cập nhật hạng mục' : 'Đã thêm hạng mục')
      requestClose()
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể lưu hạng mục'),
  })

  const fieldError = (name: keyof FormValues) =>
    errors[name] ? <p id={`deliverable-${name}-error`} className="text-sm text-destructive">{errors[name]?.message}</p> : null
  const a11y = (name: keyof FormValues) => ({
    'aria-invalid': !!errors[name],
    'aria-describedby': errors[name] ? `deliverable-${name}-error` : undefined,
  })

  return (
    <>
      <Dialog open={open} onOpenChange={v => { if (!v) requestClose() }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Sửa hạng mục' : 'Thêm hạng mục'}</DialogTitle>
            <DialogDescription>Hạng mục bàn giao và số vòng sửa miễn phí của nó.</DialogDescription>
          </DialogHeader>
          <form onSubmit={handleSubmit(v => mutation.mutate(v))} className="space-y-5 py-2" noValidate>
            {!editing && (
              <div className="space-y-2">
                <Label htmlFor="deliverable-service">Dịch vụ mẫu</Label>
                <SearchCombobox
                  id="deliverable-service"
                  value={serviceId === NO_SERVICE ? '' : serviceId}
                  options={serviceOptions}
                  search={serviceSearch}
                  onSearchChange={setServiceSearch}
                  onSelect={o => onServiceChange(o?.value ?? NO_SERVICE)}
                  placeholder="Không dùng dịch vụ mẫu"
                  clearLabel="Không dùng dịch vụ mẫu"
                  searchPlaceholder="Tìm dịch vụ…"
                  emptyText="Không có dịch vụ khớp"
                />
                {serviceId === NO_SERVICE && (
                  <p className="text-xs text-muted-foreground">
                    Không có dịch vụ mẫu: bạn nhập đơn giá khi tạo báo giá (hoặc trong bản nháp).
                  </p>
                )}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-[1fr_7rem]">
              <div className="space-y-2">
                <Label htmlFor="deliverable-name">Tên hạng mục</Label>
                <Input id="deliverable-name" {...a11y('name')} {...register('name')} />
                {fieldError('name')}
              </div>
              <div className="space-y-2">
                <Label htmlFor="deliverable-quantity">Số lượng</Label>
                <Input id="deliverable-quantity" type="number" min={1} {...a11y('quantity')} {...register('quantity')} />
                {fieldError('quantity')}
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="deliverable-includedRounds">Số vòng sửa miễn phí</Label>
                <Input id="deliverable-includedRounds" type="number" min={0} max={50} {...a11y('includedRounds')} {...register('includedRounds')} />
                {fieldError('includedRounds')}
              </div>
              <div className="space-y-2">
                <Label htmlFor="deliverable-extraRoundPrice">Giá vòng sửa thêm (₫)</Label>
                <Controller
                  name="extraRoundPrice"
                  control={control}
                  render={({ field }) => (
                    <MoneyInput id="deliverable-extraRoundPrice" value={field.value ?? 0} onChange={field.onChange} onBlur={field.onBlur} ref={field.ref} {...a11y('extraRoundPrice')} />
                  )}
                />
                {fieldError('extraRoundPrice')}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="deliverable-dueDate">Hạn bàn giao</Label>
              <DateInput id="deliverable-dueDate" value={watch('dueDate') ?? ''} onChange={v => setValue('dueDate', v, { shouldDirty: true })} />
            </div>

            <DialogFooter className="gap-2 pt-2">
              <Button type="button" variant="outline" onClick={requestClose}>Huỷ</Button>
              <Button type="submit" disabled={mutation.isPending}>
                {mutation.isPending ? 'Đang lưu…' : editing ? 'Lưu' : 'Thêm'}
              </Button>
            </DialogFooter>
          </form>
          {editing ? (
            <DeliverableLinks key={editing.id} deliverable={editing} onDirtyChange={reportLinksDirty} />
          ) : (
            <p className="border-t pt-4 text-xs text-muted-foreground">Lưu hạng mục trước rồi mới thêm liên kết.</p>
          )}
        </DialogContent>
      </Dialog>
      <ConfirmDialog
        open={confirmClose}
        onOpenChange={setConfirmClose}
        title="Bỏ liên kết chưa lưu?"
        description="Bạn đã sửa liên kết nhưng chưa bấm “Lưu liên kết”. Đóng lại sẽ bỏ các thay đổi đó."
        confirmLabel="Bỏ thay đổi"
        cancelLabel="Ở lại"
        destructive
        onConfirm={() => { setConfirmClose(false); onClose() }}
      />
    </>
  )
}

/** P15 D6: links of a deliverable, saved on their own (PUT …/links); flagged ones show on the client portal. */
function DeliverableLinks({ deliverable, onDirtyChange }: { deliverable: Deliverable; onDirtyChange: (dirty: boolean) => void }) {
  const qc = useQueryClient()
  // `saved` is the server's list: the dialog's `deliverable` prop is a snapshot taken when it opened
  const [saved, setSaved] = useState<ExternalLink[]>(deliverable.links ?? [])
  const [links, setLinks] = useState<ExternalLink[]>(saved)
  const dirty = JSON.stringify(links) !== JSON.stringify(saved)
  useReportDirty(dirty, onDirtyChange)
  const save = useMutation({
    mutationFn: () => api.put<{ links: ExternalLink[] }>(`/admin/deliverables/${deliverable.id}/links`, { links }),
    onSuccess: res => {
      setSaved(res.links)
      setLinks(res.links)
      toast.success('Đã lưu liên kết')
      return qc.invalidateQueries({ queryKey: qk.projects.all })
    },
    onError: (err: ApiError) => toast.error((err.errors ? Object.values(err.errors).flat()[0] : undefined) ?? err.detail ?? 'Không thể lưu liên kết'),
  })

  return (
    <section aria-labelledby="deliverable-links-title" className="space-y-3 border-t pt-4">
      <div>
        <h3 id="deliverable-links-title" className="text-sm font-semibold text-foreground">Liên kết</h3>
        <p className="text-xs text-muted-foreground">File Figma, thư mục Drive… cho hạng mục này.</p>
      </div>
      <LinkListEditor value={links} onChange={setLinks} allowClientVisible disabled={save.isPending} />
      <div className="flex flex-wrap items-center justify-end gap-x-3 gap-y-2">
        {dirty && <p role="status" className="text-xs text-muted-foreground">Liên kết chưa lưu</p>}
        <Button type="button" variant="outline" disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
          {save.isPending ? 'Đang lưu…' : 'Lưu liên kết'}
        </Button>
      </div>
    </section>
  )
}
