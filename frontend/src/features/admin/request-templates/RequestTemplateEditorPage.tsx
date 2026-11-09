import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ChevronDown, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TemplateFields } from '@/components/TemplateFields'
import { Tile, TileHeader } from '@/components/Tile'
import { designTypeLabel } from '@/features/portal/labels'
import { useActiveServices } from '@/features/admin/services/ServicePicker'
import type { ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import {
  FIELD_TYPE_LABEL, TEMPLATE_LIMITS, emptyDrafts, fieldsError, imagesField, moveItem, newField,
  type AnswerDraft, type AnswerDrafts,
} from '@/lib/requestTemplates'
import type { AdminRequestTemplate, RequestTemplateBody, TemplateField, TemplateFieldType } from '@/lib/requestTemplateTypes'
import { FieldEditor } from './FieldEditor'
import { useDeleteRequestTemplate, useRequestTemplate, useSaveRequestTemplate } from './useRequestTemplates'

const LIST_PATH = '/admin/settings/request-templates'
const nativeSelect =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

const blankForm = (): RequestTemplateBody => ({
  name: '', description: '', designType: 'OTHER', serviceId: null, active: true, fields: [newField('SHORT_TEXT', [])],
})

const toForm = (t: AdminRequestTemplate): RequestTemplateBody => ({
  name: t.name, description: t.description, designType: t.designType, serviceId: t.serviceId, active: t.active,
  fields: t.fields, rowVersion: t.rowVersion,
})

export default function RequestTemplateEditorPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const template = useRequestTemplate(id)
  const services = useActiveServices()
  const save = useSaveRequestTemplate(id)
  const del = useDeleteRequestTemplate()
  // null = untouched: show what the server has (or a blank form for "new")
  const [edited, setEdited] = useState<RequestTemplateBody | null>(null)
  const [serverErrors, setServerErrors] = useState<Record<string, string>>({})
  const [conflict, setConflict] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [triedSave, setTriedSave] = useState(false)

  if (id && template.isLoading) {
    return <div className="space-y-4"><Skeleton className="h-10 w-72" /><Skeleton className="h-96 w-full rounded-xl" /></div>
  }
  if (id && (template.isError || !template.data)) {
    return (
      <EmptyState icon={Trash2} title="Không tìm thấy mẫu" description="Mẫu này không còn tồn tại hoặc đã bị xoá."
        action={<Button asChild variant="outline"><Link to={LIST_PATH}>Về danh sách mẫu</Link></Button>} />
    )
  }

  const form: RequestTemplateBody = edited ?? (template.data ? toForm(template.data) : blankForm())
  const patch = (p: Partial<RequestTemplateBody>) => { setEdited({ ...form, ...p }); setServerErrors({}) }
  const setField = (i: number, f: TemplateField) => patch({ fields: form.fields.map((x, j) => (j === i ? f : x)) })
  const localFieldsError = fieldsError(form.fields)
  const nameError = serverErrors.name ?? (triedSave && form.name.trim() === '' ? 'Nhập tên mẫu' : undefined)
  const fieldsMessage = serverErrors.fields ?? localFieldsError ?? undefined
  const hasImages = !!imagesField(form.fields)

  function onSave() {
    setTriedSave(true)
    if (form.name.trim() === '' || localFieldsError) return
    save.mutate(
      { ...form, name: form.name.trim(), description: form.description.trim() },
      {
        onSuccess: saved => {
          toast.success('Đã lưu mẫu')
          setServerErrors({})
          setConflict(false)
          setEdited(null)
          qc.setQueryData(qk.requestTemplates.detail(saved.id), saved)
          if (!id) navigate(`${LIST_PATH}/${saved.id}`, { replace: true })
        },
        onError: (e: Error) => {
          const err = e as ApiError
          if (err.status === 409) { setConflict(true); toast.error('Mẫu đã được sửa ở nơi khác — tải lại trang để xem bản mới'); return }
          if (err.errors) {
            setServerErrors(Object.fromEntries(Object.entries(err.errors).map(([k, v]) => [k, v[0]])))
            toast.error('Vui lòng kiểm tra lại các trường được đánh dấu.')
          } else toast.error(err.detail ?? 'Lưu thất bại. Vui lòng thử lại.')
        },
      },
    )
  }

  function reload() {
    template.refetch().then(() => { setEdited(null); setConflict(false); setServerErrors({}) })
  }

  return (
    <div className="space-y-6">
      <Link to={LIST_PATH} className="inline-flex min-h-10 items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" /> Quay lại
      </Link>
      <PageHeader
        eyebrow="Cài đặt"
        title={id ? 'Sửa mẫu yêu cầu' : 'Mẫu yêu cầu mới'}
        description="Khách thấy mẫu này ở portal và form khách mới."
        actions={
          <>
            {id && (
              <Button type="button" variant="outline" onClick={() => setConfirmingDelete(true)}>
                <Trash2 aria-hidden="true" /> Xoá mẫu
              </Button>
            )}
            <Button type="button" disabled={save.isPending} onClick={onSave}>{save.isPending ? 'Đang lưu…' : 'Lưu mẫu'}</Button>
          </>
        }
      />

      {conflict && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-warning/30 bg-warning-soft px-4 py-3 text-sm text-warning">
          Mẫu đã được sửa ở nơi khác — tải lại trang để xem bản mới.
          <Button type="button" size="sm" variant="outline" onClick={reload}>Tải lại</Button>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] lg:items-start">
        <div className="space-y-6">
          <Tile className="space-y-5">
            <TileHeader title="Thông tin mẫu" />
            <div className="space-y-2">
              <Label htmlFor="tpl-name">Tên mẫu</Label>
              <Input id="tpl-name" value={form.name} maxLength={TEMPLATE_LIMITS.maxName}
                aria-invalid={!!nameError} aria-describedby={nameError ? 'tpl-name-error' : undefined}
                onChange={e => patch({ name: e.target.value })} />
              {nameError && <p id="tpl-name-error" className="text-sm text-destructive">{nameError}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="tpl-desc">Mô tả ngắn cho khách</Label>
              <Textarea id="tpl-desc" rows={2} value={form.description} maxLength={TEMPLATE_LIMITS.maxDescription}
                aria-invalid={!!serverErrors.description} onChange={e => patch({ description: e.target.value })} />
              {serverErrors.description && <p className="text-sm text-destructive">{serverErrors.description}</p>}
            </div>
            <div className="grid gap-5 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="tpl-design">Loại thiết kế khi tạo dự án</Label>
                <select id="tpl-design" className={nativeSelect} value={form.designType}
                  onChange={e => patch({ designType: e.target.value as RequestTemplateBody['designType'] })}>
                  {Object.entries(designTypeLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
                {serverErrors.designType && <p className="text-sm text-destructive">{serverErrors.designType}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="tpl-service">Dịch vụ khi tạo hạng mục</Label>
                <select id="tpl-service" className={nativeSelect} value={form.serviceId ?? ''}
                  aria-describedby="tpl-service-hint"
                  onChange={e => patch({ serviceId: e.target.value || null })}>
                  <option value="">Không gắn dịch vụ</option>
                  {/* a linked service that was archived since: keep it visible instead of silently unlinking */}
                  {form.serviceId && !services.data?.some(s => s.id === form.serviceId) && (
                    <option value={form.serviceId}>{template.data?.serviceName ?? 'Dịch vụ đã ẩn'}</option>
                  )}
                  {services.data?.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                </select>
                {serverErrors.serviceId && <p className="text-sm text-destructive">{serverErrors.serviceId}</p>}
              </div>
            </div>
            <p id="tpl-service-hint" className="-mt-2 text-xs text-muted-foreground">
              Khi chuyển hồ sơ khách mới thành dự án, hạng mục được tạo từ dịch vụ này (giá, số vòng sửa, bước công việc mẫu).
            </p>
            <label className="flex min-h-10 cursor-pointer items-center gap-2 text-sm text-foreground">
              <input type="checkbox" className="h-4 w-4" checked={form.active} onChange={e => patch({ active: e.target.checked })} />
              Đang dùng (hiện cho khách)
            </label>
          </Tile>

          <Tile className="space-y-4">
            <TileHeader title="Câu hỏi" meta={`${form.fields.length}/${TEMPLATE_LIMITS.maxFields}`} />
            {form.fields.length === 0 ? (
              <p className="text-sm text-muted-foreground">Chưa có câu hỏi — khách chỉ điền ghi chú.</p>
            ) : (
              <ol className="space-y-3">
                {form.fields.map((f, i) => (
                  <FieldEditor key={f.key} field={f} index={i} count={form.fields.length} imagesTaken={hasImages}
                    onChange={x => setField(i, x)}
                    onMove={d => patch({ fields: moveItem(form.fields, i, d) })}
                    onRemove={() => patch({ fields: form.fields.filter((_, j) => j !== i) })} />
                ))}
              </ol>
            )}
            {fieldsMessage && <p role="alert" className="text-sm text-destructive">{fieldsMessage}</p>}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button type="button" variant="outline" disabled={form.fields.length >= TEMPLATE_LIMITS.maxFields}>
                  <Plus aria-hidden="true" /> Thêm câu hỏi <ChevronDown aria-hidden="true" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start">
                {(Object.keys(FIELD_TYPE_LABEL) as TemplateFieldType[]).map(t => (
                  <DropdownMenuItem key={t} disabled={t === 'IMAGES' && hasImages}
                    onSelect={() => patch({ fields: [...form.fields, newField(t, form.fields.map(f => f.key))] })}>
                    {FIELD_TYPE_LABEL[t]}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </Tile>
        </div>

        <Tile className="lg:sticky lg:top-20" aria-labelledby="tpl-preview-title">
          <TileHeader id="tpl-preview-title" title="Xem trước như khách thấy" />
          <p className="mt-1 text-xs text-muted-foreground">Chỉ để xem — không gửi đi đâu.</p>
          <div className="mt-5 space-y-5">
            <div>
              <p className="font-display text-base font-bold text-foreground">{form.name.trim() || 'Tên mẫu'}</p>
              {form.description.trim() && <p className="mt-1 text-sm text-muted-foreground">{form.description}</p>}
            </div>
            {/* remount (fresh answers) when the question list changes shape */}
            <Preview key={form.fields.map(f => `${f.key}:${f.type}`).join('|')} fields={form.fields} />
          </div>
        </Tile>
      </div>

      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        destructive
        title={`Xoá mẫu "${template.data?.name ?? form.name}"?`}
        description="Yêu cầu khách đã gửi theo mẫu này vẫn giữ nguyên nội dung."
        confirmLabel="Xoá mẫu"
        pending={del.isPending}
        onConfirm={() => id && del.mutate(id, {
          onSuccess: () => { toast.success('Đã xoá mẫu'); navigate(LIST_PATH, { replace: true }) },
          onError: () => toast.error('Xoá thất bại. Vui lòng thử lại.'),
        })}
      />
    </div>
  )
}

function Preview({ fields }: { fields: TemplateField[] }) {
  const [drafts, setDrafts] = useState<AnswerDrafts>(() => emptyDrafts(fields))
  const [files, setFiles] = useState<File[]>([])
  if (fields.length === 0) return <p className="text-sm text-muted-foreground">Chưa có câu hỏi.</p>
  return (
    <TemplateFields
      idPrefix="preview" fields={fields} drafts={drafts} files={files} onFilesChange={setFiles} maxImages={3}
      onChange={(key: string, d: AnswerDraft) => setDrafts(cur => ({ ...cur, [key]: d }))}
    />
  )
}
