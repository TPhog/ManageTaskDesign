import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { TriangleAlert } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { MoneyInput } from '@/components/MoneyInput'
import { MoneyText } from '@/components/MoneyText'
import type { ApiError } from '@/lib/api'
import type { AdditionalLineKind } from '@/lib/revisionTypes'
import { ServicePicker } from '@/features/admin/services/ServicePicker'
import {
  additionalQuotationBody,
  errorText,
  linePriceError,
  pickService,
  pricedLinesFromPreview,
  zeroPricedLineNames,
  type PricedLine,
} from './revisionUtils'
import { useAdditionalQuotationPreview, useCreateAdditionalQuotation } from './useRevisions'

const kindLabel: Record<AdditionalLineKind, string> = {
  ROUND: 'Vòng sửa thêm',
  NEW_DELIVERABLE: 'Hạng mục mới',
  SCOPE_CHANGE: 'Đổi phạm vi',
}

const lineKey = (p: PricedLine) => p.line.revisionItemIds.join('-')

interface Props {
  projectId: string
  revisionItemIds: string[]
  open: boolean
  onClose: () => void
  onCreated?: () => void
}

/** Plan 09: price every line of an additional quotation (service for new deliverables, editable price) before the draft exists. */
export function AdditionalQuotationDialog({ projectId, revisionItemIds, open, onClose, onCreated }: Props) {
  const navigate = useNavigate()
  const preview = useAdditionalQuotationPreview(projectId, revisionItemIds, open)
  const create = useCreateAdditionalQuotation(projectId)
  const [priced, setPriced] = useState<PricedLine[]>([])

  // Reset on close; on a refetch / new selection keep what was typed for lines that are still there (matched by their items).
  useEffect(() => {
    if (!open) { setPriced([]); return }
    const data = preview.data
    if (!data) return
    setPriced(prev => pricedLinesFromPreview(data).map(fresh => {
      const old = prev.find(p => lineKey(p) === lineKey(fresh))
      return old ? { ...fresh, serviceId: old.serviceId, serviceName: old.serviceName, unitPrice: old.unitPrice } : fresh
    }))
  }, [open, preview.data])

  const update = (i: number, next: PricedLine) => setPriced(ps => ps.map((p, j) => (j === i ? next : p)))
  // never show rows from an earlier selection or while the preview is failing
  const rows = open && preview.data && !preview.error ? priced : []
  const zeroNames = zeroPricedLineNames(rows)
  const previewError = preview.error as ApiError | null

  function submit() {
    create.mutate(additionalQuotationBody(revisionItemIds, priced), {
      onSuccess: res => {
        onCreated?.()
        onClose()
        toast.success('Đã tạo báo giá phát sinh (nháp)')
        navigate(`/admin/quotations/${res.id}`)
      },
      onError: err => toast.error(errorText(err as ApiError, 'Không thể tạo báo giá phát sinh')),
    })
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose() }}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Tạo báo giá phát sinh</DialogTitle>
          <DialogDescription>Kiểm tra đơn giá từng dòng. Bạn vẫn sửa được trong bản nháp trước khi gửi.</DialogDescription>
        </DialogHeader>

        {preview.isLoading && (
          <div className="space-y-3">
            {[0, 1, 2].map(k => <Skeleton key={k} className="h-24 w-full rounded-lg" />)}
          </div>
        )}
        {previewError && (
          <p className="flex flex-wrap items-center gap-1 text-sm text-muted-foreground">
            {previewError.status === 422 ? errorText(previewError, 'Không tải được các dòng báo giá.') : 'Không tải được các dòng báo giá.'}
            <Button variant="link" className="px-0" onClick={() => preview.refetch()}>Thử lại</Button>
          </p>
        )}

        {rows.length > 0 && (
          <>
            <ol className="space-y-3">
              {rows.map((p, i) => {
                const { line } = p
                const error = linePriceError(p.unitPrice)
                return (
                  <li
                    key={lineKey(p)}
                    className={`space-y-3 rounded-lg border p-3 ${p.unitPrice === 0 ? 'border-amber-300 bg-amber-50/40' : ''}`}
                  >
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-baseline gap-2">
                        <span className="text-xs text-muted-foreground">{kindLabel[line.kind]}</span>
                        <span className="min-w-0 break-words text-sm font-medium">{p.serviceName ?? line.description}</span>
                      </div>
                      {line.kind === 'NEW_DELIVERABLE' && p.serviceName ? (
                        <p className="text-xs text-muted-foreground whitespace-pre-line">{line.description}</p>
                      ) : (
                        line.details && <p className="text-xs text-muted-foreground whitespace-pre-line">{line.details}</p>
                      )}
                    </div>
                    <div className="grid gap-3 sm:grid-cols-[1fr_11rem]">
                      {line.kind === 'NEW_DELIVERABLE' ? (
                        <div className="min-w-0 space-y-1">
                          <Label htmlFor={`svc-${i}`} className="text-xs">Dịch vụ mẫu</Label>
                          <ServicePicker
                            id={`svc-${i}`}
                            value={p.serviceId ?? ''}
                            selectedLabel={p.serviceName ?? undefined}
                            onPick={s => update(i, pickService(p, s))}
                            placeholder="Không dùng dịch vụ mẫu"
                            clearLabel="Không dùng dịch vụ mẫu"
                            ariaLabel={`Dịch vụ mẫu cho ${line.description}`}
                          />
                          <p className="text-xs text-muted-foreground">
                            Khi khách chấp nhận, mục này thành một hạng mục trong tab Deliverables (có quota và bước mẫu của dịch vụ).
                          </p>
                        </div>
                      ) : (
                        <div className="hidden sm:block" />
                      )}
                      <div className="space-y-1">
                        <Label htmlFor={`price-${i}`} className="text-xs">Đơn giá (₫)</Label>
                        <MoneyInput
                          id={`price-${i}`}
                          aria-label={`Đơn giá ${line.description}`}
                          value={p.unitPrice}
                          onChange={n => update(i, { ...p, unitPrice: n })}
                        />
                        {error && <p className="text-xs text-destructive">{error}</p>}
                        {p.unitPrice === 0 && <p className="text-xs text-amber-800">Dòng này đang 0 ₫</p>}
                        {line.kind === 'ROUND' && line.suggestedUnitPrice > 0 && (
                          <p className="text-xs text-muted-foreground">Gợi ý: <MoneyText amount={line.suggestedUnitPrice} />/vòng</p>
                        )}
                      </div>
                    </div>
                  </li>
                )
              })}
            </ol>
            <p className="text-sm">
              Tạm tính (chưa giảm giá/VAT): <span className="font-medium"><MoneyText amount={rows.reduce((s, p) => s + p.unitPrice, 0)} /></span>
            </p>
            {zeroNames.length > 0 && (
              <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                <p>Còn {zeroNames.length} dòng 0 ₫: {zeroNames.join(', ')}. Vẫn tạo được — nhập giá sau trong bản nháp.</p>
              </div>
            )}
          </>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Huỷ</Button>
          <Button
            onClick={submit}
            disabled={preview.isLoading || !!preview.error || rows.length === 0
              || rows.some(p => linePriceError(p.unitPrice)) || create.isPending}
          >
            {create.isPending ? 'Đang tạo…' : 'Tạo báo giá nháp'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
