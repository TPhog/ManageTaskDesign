import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ImagePicker } from '@/components/ImagePicker'
import { MAX_PORTAL_IMAGES } from '@/lib/images'
import type { PortalDeliverable, PortalProjectListItem, PortalRequest } from '@/lib/portalTypes'
import { REQUEST_MAX_LENGTH, quotaLine, requestErrorMessage, requestSchema, type RequestFormValues } from './requests'
import { usePortalProject, useSendPortalRequest } from './usePortal'

// Native <select>: the phone's own picker is the most usable control on mobile (docs/08 POR-05)
const selectClass =
  'flex h-11 w-full rounded-md border border-input bg-card px-3 py-2 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 md:text-sm'

export interface SentRequestSummary {
  request: PortalRequest
  imageCount: number
  /** free revisions left before the designer triages this request; null = project-wide request */
  remainingRounds: number | null
}

export interface PortalRequestFormProps {
  /** open projects the client may write to; the project select is hidden when there is exactly one */
  projects: Pick<PortalProjectListItem, 'id' | 'name'>[]
  defaultProjectId?: string
  /** card dialog: fixed product, its select hidden */
  fixedDeliverable?: Pick<PortalDeliverable, 'id' | 'name' | 'includedRounds' | 'remainingRounds'>
  initialContent?: string
  /** P13 hook: controls above the text box (e.g. request templates) */
  renderBeforeContent?: (api: { setContent: (text: string) => void; projectId: string; deliverableId: string }) => ReactNode
  submitLabel?: string
  onSent: (summary: SentRequestSummary) => void
  onCancel?: () => void
}

/** CF-11/12: the one portal request form — "Gửi yêu cầu" page and the "Yêu cầu sửa" dialog of a product card. */
export function PortalRequestForm({
  projects, defaultProjectId, fixedDeliverable, initialContent, renderBeforeContent, submitLabel, onSent, onCancel,
}: PortalRequestFormProps) {
  const send = useSendPortalRequest()
  const [files, setFiles] = useState<File[]>([])
  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<RequestFormValues>({
    resolver: zodResolver(requestSchema),
    defaultValues: {
      projectId: defaultProjectId ?? (projects.length === 1 ? projects[0].id : ''),
      deliverableId: fixedDeliverable?.id ?? '',
      content: initialContent ?? '',
    },
  })
  const projectId = watch('projectId')
  const deliverableId = watch('deliverableId')
  const { data: project } = usePortalProject(fixedDeliverable ? undefined : projectId || undefined)
  const deliverables = (project?.deliverables ?? []).filter(d => d.status !== 'CANCELLED')
  const selected = fixedDeliverable ?? deliverables.find(d => d.id === deliverableId) ?? null

  const previews = useMemo(() => files.map(f => URL.createObjectURL(f)), [files])
  useEffect(() => () => previews.forEach(u => URL.revokeObjectURL(u)), [previews])

  function onSubmit(values: RequestFormValues) {
    send.mutate({ ...values, files }, {
      onSuccess: request => onSent({
        request,
        imageCount: files.length,
        remainingRounds: selected && selected.includedRounds > 0 ? selected.remainingRounds : null,
      }),
      onError: err => toast.error(requestErrorMessage(err)),
    })
  }

  const sendingLabel = files.length > 0 ? `Đang gửi ${files.length} ảnh…` : 'Đang gửi…'

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <fieldset disabled={send.isPending} className="m-0 min-w-0 space-y-5 border-0 p-0">
        {projects.length > 1 && (
          <div className="space-y-2">
            <Label htmlFor="request-project">Dự án</Label>
            <select
              id="request-project"
              className={selectClass}
              aria-invalid={!!errors.projectId}
              aria-describedby={errors.projectId ? 'request-project-error' : undefined}
              {...register('projectId', { onChange: () => setValue('deliverableId', '') })}
            >
              <option value="">Chọn dự án</option>
              {projects.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            {errors.projectId && <p id="request-project-error" className="text-sm text-destructive">{errors.projectId.message}</p>}
          </div>
        )}

        {!fixedDeliverable && (
          <div className="space-y-2">
            <Label htmlFor="request-deliverable">Sản phẩm cần sửa (tuỳ chọn)</Label>
            <select id="request-deliverable" className={selectClass} {...register('deliverableId')}>
              <option value="">Cả dự án / chưa rõ sản phẩm</option>
              {deliverables.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>
        )}

        {renderBeforeContent?.({
          setContent: text => setValue('content', text, { shouldDirty: true }),
          projectId,
          deliverableId,
        })}

        <div className="space-y-2">
          <Label htmlFor="request-content">Nội dung cần sửa</Label>
          <Textarea
            id="request-content"
            rows={6}
            maxLength={REQUEST_MAX_LENGTH}
            placeholder={'Mỗi ý một dòng, ví dụ:\nĐổi màu chữ sang xanh navy\nTăng cỡ logo thêm 10%'}
            aria-invalid={!!errors.content}
            aria-describedby={errors.content ? 'request-content-hint request-content-error' : 'request-content-hint'}
            {...register('content')}
          />
          <p id="request-content-hint" className="text-xs text-muted-foreground">Mỗi ý một dòng giúp nhà thiết kế làm nhanh hơn.</p>
          {errors.content && <p id="request-content-error" className="text-sm text-destructive">{errors.content.message}</p>}
        </div>

        <div className="space-y-2">
          <Label htmlFor="request-images">Ảnh minh hoạ (tuỳ chọn)</Label>
          <ImagePicker
            id="request-images"
            max={MAX_PORTAL_IMAGES - files.length}
            disabled={send.isPending || files.length >= MAX_PORTAL_IMAGES}
            onFiles={picked => setFiles(current => [...current, ...picked].slice(0, MAX_PORTAL_IMAGES))}
            label="Thêm ảnh"
            hint={`Ảnh chụp màn hình hoặc ảnh mẫu · tối đa ${MAX_PORTAL_IMAGES} ảnh, 20 MB/ảnh`}
          />
          {files.length > 0 && (
            <ul className="grid grid-cols-4 gap-2" aria-label="Ảnh đính kèm">
              {files.map((f, i) => (
                <li key={`${f.name}-${i}`} className="relative aspect-square overflow-hidden border border-border bg-muted">
                  <img src={previews[i]} alt={f.name} className="h-full w-full object-cover" />
                  <button
                    type="button"
                    aria-label={`Bỏ ảnh ${f.name}`}
                    onClick={() => setFiles(files.filter((_, j) => j !== i))}
                    className="absolute right-0 top-0 flex h-11 w-11 items-center justify-center bg-surface-inverse/75 text-on-inverse transition-colors hover:bg-surface-inverse focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-on-inverse-accent"
                  >
                    <X className="h-4 w-4" aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <p role="note" className="border-l-4 border-warning py-1 pl-4 font-serif text-[0.95rem] italic leading-relaxed text-foreground">
          {quotaLine(selected)}
        </p>

        <div className="space-y-2">
          <Button type="submit" size="touch" className="w-full">
            {send.isPending ? sendingLabel : submitLabel ?? 'Gửi yêu cầu'}
          </Button>
          {onCancel && (
            <Button type="button" variant="outline" className="h-11 w-full" onClick={onCancel}>Huỷ</Button>
          )}
        </div>
      </fieldset>
    </form>
  )
}
