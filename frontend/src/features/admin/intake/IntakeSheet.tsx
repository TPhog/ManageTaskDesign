import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { ImageGrid } from '@/components/ImageGrid'
import { ImageLightbox } from '@/components/ImageLightbox'
import { ReasonDialog } from '@/components/ReasonDialog'
import { StatusBadge } from '@/components/StatusBadge'
import { TemplateAnswersView } from '@/components/TemplateAnswersView'
import type { ApiError } from '@/lib/api'
import { formatDatetime } from '@/lib/format'
import type { LightboxImage } from '@/lib/imageTypes'
import { qk } from '@/lib/queryKeys'
import type { IntakeDetail } from '@/lib/requestTemplateTypes'
import { ConvertIntakeDialog } from './ConvertIntakeDialog'
import { intakeStatusBadge, intakeStatusLabel, telHref, zaloHref } from './intakeUtils'
import { useDeleteIntake, useIntake, useRejectIntake } from './useIntake'

/** Plan 13 /admin/intake/:id — the submission, its photos and the three actions (convert / reject / delete). */
export function IntakeSheet({ id, onClose }: { id: string | undefined; onClose: () => void }) {
  return (
    <Sheet open={!!id} onOpenChange={open => { if (!open) onClose() }}>
      <SheetContent side="right" className="w-full overflow-y-auto sm:max-w-2xl">
        {id && <IntakeSheetBody key={id} id={id} onClose={onClose} />}
      </SheetContent>
    </Sheet>
  )
}

function IntakeSheetBody({ id, onClose }: { id: string; onClose: () => void }) {
  const { data, isLoading, error } = useIntake(id)

  if (isLoading) {
    return (
      <div className="space-y-3 pt-6">
        <SheetTitle className="sr-only">Đang tải hồ sơ</SheetTitle>
        <Skeleton className="h-6 w-2/3" />
        <Skeleton className="h-24 w-full" />
      </div>
    )
  }
  if (error || !data) {
    return (
      <div className="pt-6">
        <SheetTitle>Không tìm thấy hồ sơ</SheetTitle>
        <p className="mt-2 text-sm text-muted-foreground">Hồ sơ có thể đã bị xoá hoặc đường dẫn không đúng.</p>
      </div>
    )
  }
  return <IntakeDetailView d={data} onClose={onClose} />
}

function IntakeDetailView({ d, onClose }: { d: IntakeDetail; onClose: () => void }) {
  const qc = useQueryClient()
  const [convertOpen, setConvertOpen] = useState(false)
  const [rejectOpen, setRejectOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [lightbox, setLightbox] = useState<number | null>(null)
  const reject = useRejectIntake(d.id)
  const del = useDeleteIntake()

  const images: LightboxImage[] = d.images.map(i => ({ id: i.id, src: i.originalUrl, thumbSrc: i.thumbUrl, alt: 'Ảnh khách gửi' }))

  function onError(err: unknown, fallback: string) {
    if ((err as ApiError).status === 409) {
      toast.error('Hồ sơ vừa được cập nhật — đã tải lại')
      void qc.invalidateQueries({ queryKey: qk.intake.all })
    } else toast.error((err as ApiError).detail ?? fallback)
  }

  return (
    <div className="space-y-5">
      <SheetHeader className="space-y-1 text-left">
        <SheetTitle>Khách mới: {d.contactName}</SheetTitle>
        <SheetDescription>{d.templateName ?? 'Yêu cầu khác'} · {formatDatetime(d.createdAt)}</SheetDescription>
      </SheetHeader>
      <StatusBadge status={intakeStatusBadge[d.status]} label={intakeStatusLabel[d.status]} />

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline" className="min-h-11"><a href={telHref(d.contactPhone)}>Gọi {d.contactPhone}</a></Button>
        <Button asChild variant="outline" className="min-h-11">
          <a href={zaloHref(d.contactPhone)} target="_blank" rel="noreferrer">Zalo</a>
        </Button>
        {d.contactEmail && <Button asChild variant="outline" className="min-h-11"><a href={`mailto:${d.contactEmail}`}>Email</a></Button>}
      </div>

      {d.matchingClients.length > 0 && (
        <div className="rounded-lg border border-warning/25 bg-warning-soft p-3 text-sm text-warning">
          <p className="font-medium">Có thể là khách cũ</p>
          <ul className="mt-1 space-y-0.5">
            {d.matchingClients.map(c => (
              <li key={c.id}>
                <Link to={`/admin/clients/${c.id}`} className="underline underline-offset-2">{c.name}</Link>
                {' '}({c.phone ?? c.email})
              </li>
            ))}
          </ul>
        </div>
      )}

      {d.templateId ? (
        <TemplateAnswersView title={d.templateName} answers={d.answers} note={d.note} />
      ) : (
        <p className="whitespace-pre-wrap rounded-lg bg-muted p-3 text-sm">{d.body}</p>
      )}

      {images.length > 0 && (
        <>
          <ImageGrid images={images} onOpen={setLightbox} />
          <ImageLightbox images={images} index={lightbox} onIndexChange={setLightbox} />
        </>
      )}

      {d.status === 'NEW' && (
        <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
          <Button variant="ghost" className="text-danger hover:text-danger" onClick={() => setDeleteOpen(true)}>Xoá</Button>
          <Button variant="outline" onClick={() => setRejectOpen(true)}>Từ chối</Button>
          <Button onClick={() => setConvertOpen(true)}>Tạo khách & dự án</Button>
        </div>
      )}

      {d.status === 'CONVERTED' && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-success-soft p-3 text-sm text-success">
          <span className="flex-1">Đã chuyển thành dự án lúc {d.convertedAt ? formatDatetime(d.convertedAt) : ''}</span>
          <Button asChild size="sm"><Link to={`/admin/projects/${d.convertedProjectId}`}>Mở dự án</Link></Button>
          <Link to={`/admin/clients/${d.convertedClientId}`} className="underline underline-offset-2">Mở khách</Link>
        </div>
      )}

      {d.status === 'REJECTED' && (
        <div className="space-y-3">
          <p className="rounded-lg border border-danger/25 bg-danger-soft p-3 text-sm text-danger">
            Đã từ chối{d.rejectReason ? `: ${d.rejectReason}` : ''}
          </p>
          <div className="flex justify-end">
            <Button variant="ghost" className="text-danger hover:text-danger" onClick={() => setDeleteOpen(true)}>Xoá</Button>
          </div>
        </div>
      )}

      <ConvertIntakeDialog detail={d} open={convertOpen} onOpenChange={setConvertOpen} />
      <ReasonDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Từ chối hồ sơ này?"
        description="Lý do chỉ để bạn ghi nhớ — khách không nhận được thông báo."
        label="Lý do (tuỳ chọn)"
        minLength={0}
        confirmLabel="Từ chối"
        pending={reject.isPending}
        onConfirm={reason => reject.mutate({ reason: reason.trim() || null, rowVersion: d.rowVersion }, {
          onSuccess: () => { setRejectOpen(false); toast.success('Đã từ chối hồ sơ') },
          onError: err => { setRejectOpen(false); onError(err, 'Không thể từ chối hồ sơ') },
        })}
      />
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Xoá hồ sơ này?"
        description="Xoá hồ sơ và ảnh đính kèm? Không khôi phục được."
        confirmLabel="Xoá"
        destructive
        pending={del.isPending}
        onConfirm={() => del.mutate(d.id, {
          onSuccess: () => { setDeleteOpen(false); onClose(); toast.success('Đã xoá hồ sơ') },
          onError: err => { setDeleteOpen(false); onError(err, 'Không thể xoá hồ sơ') },
        })}
      />
    </div>
  )
}
