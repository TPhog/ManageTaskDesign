import { useState } from 'react'
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { MoneyInput } from '@/components/MoneyInput'
import { StatusBadge } from '@/components/StatusBadge'
import type { ApiError } from '@/lib/api'
import { liveQuoteRefs, quoteRefLabel } from '@/lib/quoteRefs'
import type { ProjectDetail } from '@/lib/types'
import { linePriceError } from '@/features/admin/revisions/revisionUtils'
import { deliverablePricesBody, isFinishedDeliverable, quotationErrorMessage, quoteTargetWarnings } from './quotationUtils'
import { useCreateQuotation } from './useQuotations'

type Mode = 'all' | 'selected'

const optionClass = 'flex items-start gap-3 rounded-lg border p-3 has-checked:border-primary has-disabled:opacity-60'

export function CreateQuotationDialog({ project, open, onClose }: { project: ProjectDetail; open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const create = useCreateQuotation(project.id)
  const [mode, setMode] = useState<Mode>('all')
  const [selected, setSelected] = useState<string[]>([])
  const [title, setTitle] = useState('')
  const [acknowledged, setAcknowledged] = useState(false)
  const [prices, setPrices] = useState<Record<string, number>>({})

  const active = project.deliverables.filter(d => d.status !== 'CANCELLED')
  const finished = project.deliverables.filter(d => isFinishedDeliverable(d.status))
  const targets = mode === 'all' ? active : finished.filter(d => selected.includes(d.id))
  const warn = quoteTargetWarnings(targets)
  // QA D-02: billing a deliverable twice needs an explicit decision (BR-01: warn, do not block)
  const needsAck = warn.alreadyQuoted.length > 0
  const unpriced = targets.filter(d => !d.serviceId)
  const canSubmit = (mode === 'all' || selected.length > 0) && (!needsAck || acknowledged)
    && unpriced.every(d => !linePriceError(prices[d.id] ?? 0))

  function close() {
    setMode('all')
    setSelected([])
    setTitle('')
    setAcknowledged(false)
    setPrices({})
    onClose()
  }

  function toggle(id: string) {
    setSelected(s => (s.includes(id) ? s.filter(x => x !== id) : [...s, id]))
  }

  function submit() {
    create.mutate(
      {
        kind: 'INITIAL',
        title: title.trim() || null,
        fromDeliverables: true,
        deliverableIds: mode === 'selected' ? selected : undefined,
        deliverablePrices: deliverablePricesBody(targets, prices),
      },
      {
        onSuccess: res => {
          for (const w of res.warnings) toast.warning(w)
          toast.success('Đã tạo báo giá nháp')
          close()
          navigate(`/admin/quotations/${res.id}`)
        },
        onError: err => toast.error(quotationErrorMessage(err as ApiError, 'Không thể tạo báo giá')),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) close() }}>
      <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Tạo báo giá</DialogTitle>
          <DialogDescription>
            Mỗi deliverable thành một dòng: số lượng × giá gốc của dịch vụ. Bạn sửa được trước khi gửi.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-5 py-2">
          <fieldset className="space-y-2">
            <legend className="mb-1 text-sm font-medium text-foreground">Lập báo giá cho</legend>
            <label className={optionClass}>
              <input type="radio" name="quotation-mode" className="mt-1" checked={mode === 'all'} onChange={() => { setMode('all'); setAcknowledged(false) }} />
              <span>
                <span className="block text-sm font-medium text-foreground">Toàn bộ dự án</span>
                <span className="block text-xs text-muted-foreground">{active.length} hạng mục (trừ hạng mục đã huỷ)</span>
              </span>
            </label>
            <label className={optionClass}>
              <input
                type="radio"
                name="quotation-mode"
                className="mt-1"
                checked={mode === 'selected'}
                disabled={finished.length === 0}
                onChange={() => { setMode('selected'); setAcknowledged(false) }}
              />
              <span>
                <span className="block text-sm font-medium text-foreground">Chọn hạng mục đã xong</span>
                <span className="block text-xs text-muted-foreground">
                  {finished.length === 0
                    ? 'Chưa có hạng mục nào ở trạng thái Khách đã duyệt / Đã bàn giao'
                    : 'Báo giá riêng với số và mã QR riêng'}
                </span>
              </span>
            </label>
          </fieldset>

          {mode === 'selected' && (
            <fieldset className="space-y-2">
              <legend className="mb-1 text-sm font-medium text-foreground">Hạng mục</legend>
              {finished.map(d => {
                const live = liveQuoteRefs(d.quotedIn)
                return (
                  <label key={d.id} className="flex items-center justify-between gap-3 rounded-lg border px-3 py-2">
                    <span className="flex min-w-0 items-center gap-3">
                      <input type="checkbox" checked={selected.includes(d.id)} onChange={() => toggle(d.id)} />
                      <span className="min-w-0">
                        <span className="block text-sm text-foreground">{d.name}</span>
                        {live.length > 0 && (
                          <span className="block text-xs text-amber-800">Đã có trong {live.map(quoteRefLabel).join(', ')}</span>
                        )}
                      </span>
                    </span>
                    <StatusBadge status={d.status} />
                  </label>
                )
              })}
            </fieldset>
          )}

          {unpriced.length > 0 && (
            <fieldset className="space-y-2">
              <legend className="mb-1 text-sm font-medium text-foreground">Đơn giá hạng mục chưa có dịch vụ mẫu</legend>
              {unpriced.map(d => (
                <div key={d.id} className="grid items-center gap-2 sm:grid-cols-[1fr_11rem]">
                  <Label htmlFor={`price-${d.id}`} className="font-normal">{d.name}{d.quantity > 1 ? ` × ${d.quantity}` : ''}</Label>
                  <MoneyInput id={`price-${d.id}`} aria-label={`Đơn giá ${d.name}`} value={prices[d.id] ?? 0}
                    onChange={n => setPrices(p => ({ ...p, [d.id]: n }))} />
                  {linePriceError(prices[d.id] ?? 0) && (
                    <p className="text-xs text-destructive sm:col-span-2">{linePriceError(prices[d.id] ?? 0)}</p>
                  )}
                </div>
              ))}
              <p className="text-xs text-muted-foreground">Để trống = 0 ₫, bạn nhập sau trong bản nháp.</p>
            </fieldset>
          )}

          {needsAck && (
            <div className="space-y-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              <div className="flex items-start gap-2">
                <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                <div>
                  <p>Đã có trong báo giá đã gửi hoặc đã chấp nhận — tạo thêm sẽ tính tiền lần nữa:</p>
                  <ul className="mt-1 list-disc pl-5">
                    {warn.alreadyQuoted.map(a => (
                      <li key={a.name}>{a.name} ({a.refs.map(quoteRefLabel).join(', ')})</li>
                    ))}
                  </ul>
                  <p className="mt-1 text-xs">Muốn sửa báo giá đã gửi? Mở báo giá đó và bấm “Tạo version mới”.</p>
                </div>
              </div>
              <label className="flex items-center gap-2 font-medium">
                <input type="checkbox" checked={acknowledged} onChange={e => setAcknowledged(e.target.checked)} />
                Tôi vẫn muốn báo giá lại các hạng mục này
              </label>
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="quotation-title">Tiêu đề báo giá</Label>
            <Input
              id="quotation-title"
              value={title}
              maxLength={200}
              placeholder="Để trống để dùng tiêu đề mặc định"
              onChange={e => setTitle(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" onClick={close}>Huỷ</Button>
          <Button type="button" disabled={!canSubmit || create.isPending} onClick={submit}>
            {create.isPending ? 'Đang tạo…' : 'Tạo nháp'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
