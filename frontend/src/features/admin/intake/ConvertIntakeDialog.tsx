import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DateInput } from '@/components/DateInput'
import type { ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { IntakeDetail } from '@/lib/requestTemplateTypes'
import type { DesignType } from '@/lib/types'
import { useActiveServices } from '@/features/admin/services/ServicePicker'
import { designTypeLabel } from '@/features/portal/labels'
import { defaultConvertValues, toConvertBody, type ConvertFormValues } from './intakeUtils'
import { useConvertIntake } from './useIntake'

const nativeSelect =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50'

interface Props { detail: IntakeDetail; open: boolean; onOpenChange: (open: boolean) => void }

/** BR-136: one click makes client + project + brief + deliverable. Values are prefilled; the form remounts on every open. */
export function ConvertIntakeDialog({ detail, open, onOpenChange }: Props) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Tạo khách & dự án</DialogTitle>
          <DialogDescription>Thông tin đã điền sẵn từ hồ sơ — sửa nếu cần rồi bấm tạo.</DialogDescription>
        </DialogHeader>
        <ConvertForm detail={detail} onClose={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}

function ConvertForm({ detail, onClose }: { detail: IntakeDetail; onClose: () => void }) {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [v, setV] = useState<ConvertFormValues>(() => defaultConvertValues(detail))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [dateBad, setDateBad] = useState(false)
  const services = useActiveServices()
  const convert = useConvertIntake(detail.id)
  const set = <K extends keyof ConvertFormValues>(k: K, val: ConvertFormValues[K]) => setV(prev => ({ ...prev, [k]: val }))
  const existing = v.clientMode === 'existing'

  function submit(e: FormEvent) {
    e.preventDefault()
    // validate on submit and say what is missing (a silently disabled button explains nothing)
    const missing: Record<string, string> = {}
    if (!v.projectName.trim()) missing.projectName = 'Nhập tên dự án'
    if (existing ? !v.existingClientId : !v.clientName.trim()) missing.clientName = existing ? 'Chọn khách có sẵn' : 'Nhập tên khách'
    if (dateBad) missing.dueDate = 'Ngày không hợp lệ (dd/mm/yyyy)'
    setErrors(missing)
    if (Object.keys(missing).length > 0) return
    convert.mutate(toConvertBody(v, detail.rowVersion), {
      onSuccess: r => { toast.success('Đã tạo khách & dự án'); onClose(); navigate(`/admin/projects/${r.projectId}`) },
      onError: err => {
        const apiErr = err as ApiError
        if (apiErr.status === 409) {
          onClose()
          toast.error('Hồ sơ đã được xử lý hoặc vừa thay đổi — đã tải lại')
          void qc.invalidateQueries({ queryKey: qk.intake.all })
        } else if (apiErr.status === 404 && apiErr.code === 'NOT_FOUND') toast.error('Khách đã chọn không còn hoạt động')
        else if (apiErr.status === 404) {
          // bodiless 404: the submission itself was deleted (e.g. in another tab)
          onClose()
          toast.error('Yêu cầu này đã bị xoá')
          void qc.invalidateQueries({ queryKey: qk.intake.all })
        }
        else if (apiErr.errors) setErrors(Object.fromEntries(Object.entries(apiErr.errors).map(([k, m]) => [k, m[0] ?? ''])))
        else toast.error(apiErr.detail ?? 'Không thể tạo khách & dự án')
      },
    })
  }

  const fieldError = (k: string) => errors[k] ? <p role="alert" className="text-xs text-danger">{errors[k]}</p> : null

  return (
    <form onSubmit={submit} className="space-y-4">
      <fieldset className="space-y-2">
        <legend className="mb-1 text-sm font-medium text-foreground">Khách</legend>
        <label className="flex min-h-9 items-center gap-2 text-sm">
          <input type="radio" name="clientMode" checked={!existing} onChange={() => set('clientMode', 'new')} />
          Tạo khách mới
        </label>
        {!existing && (
          <div className="space-y-1 pl-6">
            <Label htmlFor="cv-client-name">Tên khách</Label>
            <Input id="cv-client-name" maxLength={200} value={v.clientName} onChange={e => set('clientName', e.target.value)} aria-invalid={!!errors.clientName} />
            {fieldError('clientName')}
            <p className="text-xs text-muted-foreground">
              SĐT {detail.contactPhone}{detail.contactEmail ? ` · ${detail.contactEmail}` : ''} sẽ được lưu vào hồ sơ khách
            </p>
          </div>
        )}
        <label className={`flex min-h-9 items-center gap-2 text-sm ${detail.matchingClients.length === 0 ? 'text-muted-foreground' : ''}`}>
          <input type="radio" name="clientMode" disabled={detail.matchingClients.length === 0} checked={existing} onChange={() => set('clientMode', 'existing')} />
          Khách có sẵn
        </label>
        {detail.matchingClients.length === 0 && (
          <p className="pl-6 text-xs text-muted-foreground">Chỉ hiện khách trùng số điện thoại hoặc email</p>
        )}
        {existing && (
          <div className="pl-6">
            <select aria-label="Chọn khách có sẵn" className={nativeSelect} value={v.existingClientId} onChange={e => set('existingClientId', e.target.value)}>
              {detail.matchingClients.map(c => <option key={c.id} value={c.id}>{c.name} ({c.phone ?? c.email})</option>)}
            </select>
            {fieldError('clientName')}
          </div>
        )}
      </fieldset>

      <div className="space-y-1">
        <Label htmlFor="cv-project">Tên dự án</Label>
        <Input id="cv-project" maxLength={200} value={v.projectName} onChange={e => set('projectName', e.target.value)} aria-invalid={!!errors.projectName} />
        {fieldError('projectName')}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <Label htmlFor="cv-type">Loại thiết kế</Label>
          <select id="cv-type" className={nativeSelect} value={v.designType} onChange={e => set('designType', e.target.value as DesignType)}>
            {(Object.keys(designTypeLabel) as DesignType[]).map(t => <option key={t} value={t}>{designTypeLabel[t]}</option>)}
          </select>
          {fieldError('designType')}
        </div>
        <div className="space-y-1">
          <Label htmlFor="cv-due">Hạn hoàn thành</Label>
          <DateInput id="cv-due" value={v.dueDate} onChange={d => set('dueDate', d)} onInvalid={setDateBad} />
          {fieldError('dueDate')}
        </div>
      </div>
      <div className="space-y-1">
        <Label htmlFor="cv-service">Dịch vụ</Label>
        <select
          id="cv-service"
          className={nativeSelect}
          value={v.serviceId}
          disabled={services.isLoading}
          onChange={e => {
            const s = services.data?.find(x => x.id === e.target.value)
            setV(prev => ({ ...prev, serviceId: e.target.value, deliverableName: s?.name ?? '' }))
          }}
        >
          <option value="">Không tạo hạng mục</option>
          {(services.data ?? []).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        {fieldError('serviceId')}
        <p className="text-xs text-muted-foreground">Hạng mục lấy giá, số vòng sửa và bước công việc mẫu của dịch vụ</p>
      </div>
      {v.serviceId && (
        <div className="space-y-1">
          <Label htmlFor="cv-deliverable">Tên hạng mục</Label>
          <Input id="cv-deliverable" maxLength={200} value={v.deliverableName} onChange={e => set('deliverableName', e.target.value)} />
          {fieldError('deliverableName')}
        </div>
      )}

      <DialogFooter>
        <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
        <Button type="submit" disabled={convert.isPending}>
          {convert.isPending ? 'Đang tạo…' : 'Tạo khách & dự án'}
        </Button>
      </DialogFooter>
    </form>
  )
}
