import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { FileText, Plus, Receipt } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { BillingChip } from '@/components/BillingChip'
import { ReasonDialog } from '@/components/ReasonDialog'
import { StatusBadge } from '@/components/StatusBadge'
import { ImageGrid } from '@/components/ImageGrid'
import { TemplateAnswersView } from '@/components/TemplateAnswersView'
import { ImageLightbox } from '@/components/ImageLightbox'
import type { LightboxImage } from '@/lib/imageTypes'
import { api, type ApiError } from '@/lib/api'
import { formatDate, formatDatetime } from '@/lib/format'
import { qk } from '@/lib/queryKeys'
import { itemTypeLabel, requestSourceLabel, requestStatusLabel } from '@/lib/revisionLabels'
import type { RequestDetail, RoundInput } from '@/lib/revisionTypes'
import type { ProjectDetail } from '@/lib/types'
import { templateNote } from '@/features/admin/intake/intakeUtils'
import { todayVn } from '@/features/admin/tasks/taskUtils'
import { AdditionalQuotationDialog } from '@/features/admin/revisions/AdditionalQuotationDialog'
import { RoundEditor } from '@/features/admin/revisions/RoundEditor'
import { cleanRounds, emptyRound, errorText, itemsFromRequestText, quotableItems } from '@/features/admin/revisions/revisionUtils'
import {
  useDeclineRequest,
  useRequest,
  useTriageRequest,
} from '@/features/admin/revisions/useRevisions'

/** docs/08 §2.2 /admin/requests/:id — request content on top, triage builder or outcome below. */
export function RequestSheet({ requestId, onClose }: { requestId: string | undefined; onClose: () => void }) {
  return (
    <Sheet open={!!requestId} onOpenChange={open => { if (!open) onClose() }}>
      <SheetContent side="right" className="w-full overflow-y-auto sm:max-w-2xl">
        {requestId && <RequestSheetBody key={requestId} requestId={requestId} />}
      </SheetContent>
    </Sheet>
  )
}

function RequestSheetBody({ requestId }: { requestId: string }) {
  const { data, isLoading, error } = useRequest(requestId)

  if (isLoading) {
    return (
      <div className="space-y-3 pt-6">
        <SheetTitle className="sr-only">Đang tải yêu cầu</SheetTitle>
        <Skeleton className="h-6 w-2/3" />
        <Skeleton className="h-24 w-full" />
      </div>
    )
  }
  if (error || !data) {
    return (
      <div className="pt-6">
        <SheetTitle>Không tìm thấy yêu cầu</SheetTitle>
        <p className="mt-2 text-sm text-muted-foreground">Yêu cầu có thể đã bị xoá hoặc đường dẫn không đúng.</p>
      </div>
    )
  }

  const r = data.request
  return (
    <div className="space-y-5">
      <SheetHeader className="space-y-1 pr-10 text-left">
        <SheetTitle>Yêu cầu từ {r.clientName}</SheetTitle>
        <SheetDescription>
          <Link to={`/admin/projects/${r.projectId}?tab=revisions`} className="link-inline">{r.projectName}</Link>
          {' · '}{requestSourceLabel[r.source]} · {formatDatetime(r.createdAt)}
        </SheetDescription>
      </SheetHeader>
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={r.status} label={requestStatusLabel[r.status]} />
        {r.deliverableName && <span className="text-xs text-muted-foreground">Hạng mục khách chọn: {r.deliverableName}</span>}
      </div>
      {r.answers.length > 0 ? (
        <>
          <TemplateAnswersView title={r.templateName} answers={r.answers} note={templateNote(r.content)} />
          <details className="text-sm">
            <summary className="cursor-pointer text-muted-foreground">Xem dạng văn bản</summary>
            <p className="mt-2 whitespace-pre-wrap rounded-lg bg-muted p-3 text-foreground">{r.content}</p>
          </details>
        </>
      ) : (
        <p className="whitespace-pre-wrap rounded-lg bg-muted p-3 text-sm text-foreground">{r.content}</p>
      )}
      <RequestAttachments attachments={r.attachments} />
      {r.status === 'NEW' && <TriagePanel detail={data} />}
      {r.status === 'DECLINED' && (
        <p className="rounded-lg border border-danger/30 bg-danger-soft p-3 text-sm text-danger">Lý do từ chối: {r.declineReason}</p>
      )}
      {r.status === 'TRIAGED' && <TriagedSummary detail={data} />}
    </div>
  )
}

function TriagePanel({ detail }: { detail: RequestDetail }) {
  const r = detail.request
  const [rounds, setRounds] = useState<RoundInput[]>(() => [{ ...emptyRound(r.deliverableId, todayVn()), items: itemsFromRequestText(r.content) }])
  const [declineOpen, setDeclineOpen] = useState(false)
  const triage = useTriageRequest(r.id)
  const decline = useDeclineRequest(r.id)
  const { data: project } = useQuery({
    queryKey: qk.projects.detail(r.projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${r.projectId}`),
  })
  const deliverables = (project?.deliverables ?? []).filter(d => d.status !== 'CANCELLED')

  function save() {
    const cleaned = cleanRounds(rounds)
    if (cleaned.length === 0) { toast.error('Nhập ít nhất 1 mục sửa'); return }
    triage.mutate({ rounds: cleaned, rowVersion: r.rowVersion }, {
      onSuccess: () => toast.success('Đã tiếp nhận yêu cầu'),
      onError: err => toast.error(errorText(err as ApiError, 'Không thể tiếp nhận yêu cầu')),
    })
  }

  return (
    <section aria-label="Tiếp nhận yêu cầu" className="space-y-3">
      <h3 className="font-display text-title font-bold text-foreground">Tách thành vòng sửa</h3>
      {rounds[0] && rounds[0].items.length > 1 && (
        <p className="text-xs text-muted-foreground">Đã tách {rounds[0].items.length} mục từ nội dung — sửa hoặc xoá nếu chưa đúng</p>
      )}
      {r.content.split('\n').filter(l => l.trim()).length >= 2 && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => setRounds(prev => prev.map((x, k) => (k === 0 ? { ...x, items: itemsFromRequestText(r.content) } : x)))}
        >
          Tạo lại mục từ nội dung yêu cầu
        </Button>
      )}
      {rounds.map((round, i) => (
        <RoundEditor
          key={i}
          index={i}
          value={round}
          deliverables={deliverables}
          onChange={next => setRounds(rounds.map((x, k) => (k === i ? next : x)))}
          onRemove={rounds.length > 1 ? () => setRounds(rounds.filter((_, k) => k !== i)) : undefined}
        />
      ))}
      <Button type="button" variant="outline" size="sm" onClick={() => setRounds([...rounds, emptyRound(null, todayVn())])}>
        <Plus aria-hidden="true" />
        Thêm vòng cho hạng mục khác
      </Button>
      <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
        <Button type="button" variant="outline" className="text-danger hover:bg-danger-soft hover:text-danger" onClick={() => setDeclineOpen(true)}>
          Từ chối
        </Button>
        <Button type="button" disabled={triage.isPending} onClick={save}>
          {triage.isPending ? 'Đang lưu…' : 'Lưu & tạo vòng sửa'}
        </Button>
      </div>
      <ReasonDialog
        open={declineOpen}
        onOpenChange={setDeclineOpen}
        title="Từ chối yêu cầu"
        description="Khách sẽ thấy trạng thái Từ chối kèm lý do này trong Portal."
        label="Lý do (khách sẽ thấy)"
        confirmLabel="Từ chối yêu cầu"
        destructive
        pending={decline.isPending}
        onConfirm={reason => decline.mutate({ reason, rowVersion: r.rowVersion }, {
          onSuccess: () => { setDeclineOpen(false); toast.success('Đã từ chối yêu cầu') },
          onError: err => toast.error(errorText(err as ApiError, 'Không thể từ chối yêu cầu')),
        })}
      />
    </section>
  )
}

/** docs/08 §2.3 step 4: after saving, offer the additional quotation when chargeable items are waiting. */
function TriagedSummary({ detail }: { detail: RequestDetail }) {
  const r = detail.request
  const quotable = quotableItems(detail.rounds)
  const [quoteOpen, setQuoteOpen] = useState(false)

  return (
    <section aria-label="Vòng sửa đã tạo" className="space-y-3">
      {quotable.length > 0 && (
        <div className="flex flex-col gap-3 rounded-lg border border-warning/30 bg-warning-soft p-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm font-medium text-warning">{quotable.length} mục tính phí chưa báo giá</p>
          <Button size="sm" onClick={() => setQuoteOpen(true)}>
            <Receipt aria-hidden="true" />
            Tạo báo giá phát sinh
          </Button>
        </div>
      )}
      {detail.rounds.map(round => (
        <div key={round.id} className="rounded-lg border p-3">
          <p className="text-sm font-medium text-foreground">
            Vòng {round.roundNo} — {round.deliverableName ?? 'Cấp dự án'}
            <span className="ml-2 text-xs font-normal text-muted-foreground">nhận {formatDate(round.receivedAt)}</span>
          </p>
          <ul className="mt-2 space-y-1.5">
            {round.items.map(item => (
              <li key={item.id} className="flex flex-wrap items-center gap-2 text-sm">
                <span className="min-w-0 flex-1">{item.description}</span>
                <span className="text-xs text-muted-foreground">{itemTypeLabel[item.type]}</span>
                <BillingChip billing={item.billing} />
              </li>
            ))}
          </ul>
        </div>
      ))}
      <Button asChild variant="outline" size="sm">
        <Link to={`/admin/projects/${r.projectId}?tab=revisions`}>Mở tab Yêu cầu & vòng sửa</Link>
      </Button>
      <AdditionalQuotationDialog
        projectId={r.projectId}
        revisionItemIds={quotable.map(i => i.id)}
        open={quoteOpen}
        onClose={() => setQuoteOpen(false)}
      />
    </section>
  )
}

/** Client photos (plan 08): thumbnails, and the unwatermarked original in the viewer; legacy files stay a name list. */
function RequestAttachments({ attachments }: { attachments: RequestDetail['request']['attachments'] }) {
  const [open, setOpen] = useState<number | null>(null)
  if (attachments.length === 0) return null
  const images: LightboxImage[] = attachments
    .filter(a => a.thumbUrl && a.originalUrl)
    .map(a => ({ id: a.id, src: a.originalUrl!, thumbSrc: a.thumbUrl!, alt: a.originalName }))
  const others = attachments.filter(a => !a.thumbUrl)
  return (
    <div className="space-y-2">
      {images.length > 0 && <ImageGrid images={images} onOpen={setOpen} className="grid-cols-4" />}
      {others.length > 0 && (
        <ul className="space-y-1 text-sm">
          {others.map(a => (
            <li key={a.id} className="flex items-center gap-2 text-muted-foreground">
              <FileText className="h-4 w-4" aria-hidden="true" />
              {a.originalName}
            </li>
          ))}
        </ul>
      )}
      <ImageLightbox images={images} index={open} onIndexChange={setOpen} />
    </div>
  )
}
