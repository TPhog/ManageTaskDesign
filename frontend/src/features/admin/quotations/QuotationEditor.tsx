import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, Send, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import type { ApiError } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { QuotationDetail, QuotationItem, QuotationItemSource, UpdateQuotationBody } from '@/lib/quotationTypes'
import { computeTotals, kindLabel, quotationErrorMessage } from './quotationUtils'
import { useDeleteQuotation, useQuotationCommand, useUpdateQuotation } from './useQuotations'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

interface Row {
  key: string
  id: string | null
  description: string
  details: string
  quantity: number
  unitPrice: number
  sourceType: QuotationItemSource
  sourceId: string | null
}

interface Header {
  title: string
  validityDays: number
  discountAmount: number
  vatPercent: number
  depositPercent: number
  terms: string
  notesToClient: string
  internalNotes: string
}

const toRows = (items: QuotationItem[]): Row[] =>
  items.map(i => ({
    key: i.id, id: i.id, description: i.description, details: i.details, quantity: i.quantity,
    unitPrice: i.unitPrice, sourceType: i.sourceType, sourceId: i.sourceId,
  }))

const toHeader = (q: QuotationDetail): Header => ({
  title: q.title, validityDays: q.validityDays, discountAmount: q.discountAmount, vatPercent: q.vatPercent,
  depositPercent: q.depositPercent, terms: q.terms, notesToClient: q.notesToClient, internalNotes: q.internalNotes,
})

/** '' or garbage → 0; decimals are cut (money and percentages are whole numbers). */
const toInt = (value: string) => Math.trunc(Number(value)) || 0

function Field({ id, label, hint, className, children }: { id: string; label: string; hint?: string; className?: string; children: ReactNode }) {
  return (
    <div className={cn('space-y-2', className)}>
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  )
}

function TotalLine({ label, strong, testId, children }: { label: string; strong?: boolean; testId?: string; children: ReactNode }) {
  return (
    <div className={cn('flex items-center justify-between gap-3', strong && 'border-t pt-2 text-base font-semibold')}>
      <dt className={strong ? 'text-foreground' : 'text-muted-foreground'}>{label}</dt>
      <dd data-testid={testId} className="tabular-nums text-foreground">{children}</dd>
    </div>
  )
}

export function QuotationEditor({ quotation }: { quotation: QuotationDetail }) {
  const navigate = useNavigate()
  const update = useUpdateQuotation(quotation.id)
  const command = useQuotationCommand(quotation.id)
  const remove = useDeleteQuotation(quotation.id)
  const [header, setHeader] = useState<Header>(() => toHeader(quotation))
  const [rows, setRows] = useState<Row[]>(() => toRows(quotation.items))
  const [confirmSend, setConfirmSend] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const preview = computeTotals(rows, header.discountAmount, header.vatPercent, header.depositPercent)
  const invalid = header.title.trim() === '' || rows.some(r => r.description.trim() === '')
  const busy = update.isPending || command.isPending || remove.isPending

  const setField = <K extends keyof Header>(key: K, value: Header[K]) => setHeader(h => ({ ...h, [key]: value }))
  const setRow = (key: string, patch: Partial<Row>) => setRows(rs => rs.map(r => (r.key === key ? { ...r, ...patch } : r)))
  const addRow = () =>
    setRows(rs => [...rs, { key: crypto.randomUUID(), id: null, description: '', details: '', quantity: 1, unitPrice: 0, sourceType: 'MANUAL', sourceId: null }])
  const removeRow = (key: string) => setRows(rs => rs.filter(r => r.key !== key))

  const body = (): UpdateQuotationBody => ({
    ...header,
    title: header.title.trim(),
    items: rows.map(r => ({
      id: r.id, description: r.description.trim(), details: r.details, quantity: r.quantity,
      unitPrice: r.unitPrice, sourceType: r.sourceType, sourceId: r.sourceId,
    })),
    rowVersion: quotation.rowVersion,
  })

  /** The server recalculates money and gives new rows their ids — adopt its version of the draft. */
  const adopt = (saved: QuotationDetail) => {
    setHeader(toHeader(saved))
    setRows(toRows(saved.items))
  }

  function save() {
    update.mutate(body(), {
      onSuccess: saved => {
        adopt(saved)
        toast.success('Đã lưu báo giá')
      },
      onError: err => toast.error(quotationErrorMessage(err as ApiError, 'Không thể lưu báo giá')),
    })
  }

  async function saveAndSend() {
    try {
      const saved = await update.mutateAsync(body())
      const sent = await command.mutateAsync({ action: 'send', body: { rowVersion: saved.rowVersion } })
      toast.success(`Đã gửi báo giá ${sent.number}`)
    } catch (err) {
      toast.error(quotationErrorMessage(err as ApiError, 'Không thể gửi báo giá'))
    } finally {
      setConfirmSend(false)
    }
  }

  function deleteDraft() {
    remove.mutate(undefined, {
      onSuccess: () => {
        toast.success('Đã xóa báo giá nháp')
        navigate(`/admin/projects/${quotation.projectId}`)
      },
      onError: err => toast.error(quotationErrorMessage(err as ApiError, 'Không thể xóa báo giá')),
    })
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
      <div className="space-y-6">
        <div className={panelClass}>
          <div className="mb-5 flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight text-foreground">Soạn báo giá</h1>
            <StatusBadge status="DRAFT" />
            <span className="text-sm text-muted-foreground">
              {kindLabel[quotation.kind]} · v{quotation.version}{quotation.number ? ` · ${quotation.number}` : ''}
            </span>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="q-title" label="Tiêu đề" className="sm:col-span-2">
              <Input id="q-title" value={header.title} maxLength={200} onChange={e => setField('title', e.target.value)} />
            </Field>
            <Field id="q-validity" label="Hiệu lực (ngày)" hint="Tính từ ngày gửi">
              <Input id="q-validity" type="number" min={1} max={365} value={header.validityDays} onChange={e => setField('validityDays', toInt(e.target.value))} />
            </Field>
            <Field id="q-discount" label="Giảm giá (₫)">
              <Input id="q-discount" type="number" min={0} step={1000} value={header.discountAmount} onChange={e => setField('discountAmount', toInt(e.target.value))} />
            </Field>
            <Field id="q-vat" label="VAT (%)">
              <Input id="q-vat" type="number" min={0} max={20} value={header.vatPercent} onChange={e => setField('vatPercent', toInt(e.target.value))} />
            </Field>
            <Field id="q-deposit" label="Cọc (%)">
              <Input id="q-deposit" type="number" min={0} max={100} value={header.depositPercent} onChange={e => setField('depositPercent', toInt(e.target.value))} />
            </Field>
          </div>
        </div>

        <div className={panelClass}>
          <div className="mb-4 flex items-center justify-between gap-3">
            <h2 className="text-sm font-semibold text-foreground">Dòng báo giá</h2>
            <Button type="button" size="sm" variant="outline" onClick={addRow}>
              <Plus aria-hidden="true" />
              Thêm dòng
            </Button>
          </div>
          {rows.length === 0 ? (
            <p className="text-sm text-muted-foreground">Chưa có dòng nào — thêm ít nhất 1 dòng trước khi gửi.</p>
          ) : (
            <div className="space-y-3">
              {rows.map((r, i) => (
                <div key={r.key} className="grid items-end gap-3 rounded-lg border p-3 sm:grid-cols-[1fr_5rem_9rem_8rem_auto]">
                  <Input aria-label={`Mô tả dòng ${i + 1}`} placeholder="Mô tả" value={r.description} maxLength={500} onChange={e => setRow(r.key, { description: e.target.value })} />
                  <Input aria-label={`Số lượng dòng ${i + 1}`} type="number" min={1} value={r.quantity} onChange={e => setRow(r.key, { quantity: toInt(e.target.value) })} />
                  <Input aria-label={`Đơn giá dòng ${i + 1}`} type="number" min={0} step={1000} value={r.unitPrice} onChange={e => setRow(r.key, { unitPrice: toInt(e.target.value) })} />
                  <span className="pb-2 text-right text-sm tabular-nums text-foreground"><MoneyText amount={r.quantity * r.unitPrice} /></span>
                  <Button type="button" variant="ghost" size="icon" aria-label={`Xóa dòng ${i + 1}`} onClick={() => removeRow(r.key)}>
                    <Trash2 aria-hidden="true" />
                  </Button>
                  <Textarea
                    aria-label={`Chi tiết dòng ${i + 1}`}
                    placeholder="Chi tiết (tùy chọn)"
                    rows={2}
                    className="sm:col-span-5"
                    value={r.details}
                    onChange={e => setRow(r.key, { details: e.target.value })}
                  />
                </div>
              ))}
            </div>
          )}
        </div>

        <div className={`${panelClass} space-y-4`}>
          <Field id="q-terms" label="Điều khoản">
            <Textarea id="q-terms" rows={3} value={header.terms} onChange={e => setField('terms', e.target.value)} />
          </Field>
          <Field id="q-notes" label="Ghi chú cho khách">
            <Textarea id="q-notes" rows={2} value={header.notesToClient} onChange={e => setField('notesToClient', e.target.value)} />
          </Field>
          <Field id="q-internal" label="Ghi chú nội bộ" hint="Khách không thấy">
            <Textarea id="q-internal" rows={2} value={header.internalNotes} onChange={e => setField('internalNotes', e.target.value)} />
          </Field>
        </div>
      </div>

      <aside className="space-y-4 lg:sticky lg:top-6 lg:self-start">
        <div className={panelClass}>
          <h2 className="mb-3 text-sm font-semibold text-foreground">Tổng tiền (xem trước)</h2>
          <dl className="space-y-2 text-sm">
            <TotalLine label="Tạm tính"><MoneyText amount={preview.subtotal} /></TotalLine>
            <TotalLine label="Giảm giá">− <MoneyText amount={header.discountAmount} /></TotalLine>
            <TotalLine label={`VAT ${header.vatPercent}%`}><MoneyText amount={preview.vatAmount} /></TotalLine>
            <TotalLine label="Tổng cộng" strong testId="draft-total"><MoneyText amount={preview.total} /></TotalLine>
            <TotalLine label={`Cọc ${header.depositPercent}%`}><MoneyText amount={preview.depositAmount} /></TotalLine>
          </dl>
          {header.discountAmount > preview.subtotal && (
            <p className="mt-2 text-sm text-destructive">Giảm giá vượt quá tạm tính</p>
          )}
          <p className="mt-3 text-xs text-muted-foreground">Máy chủ tính lại khi lưu (làm tròn nửa lên đến đồng).</p>
        </div>
        <div className="flex flex-col gap-2">
          <Button onClick={() => setConfirmSend(true)} disabled={invalid || rows.length === 0 || busy}>
            <Send aria-hidden="true" />
            Gửi báo giá
          </Button>
          <Button variant="outline" onClick={save} disabled={invalid || busy}>Lưu nháp</Button>
          <Button variant="ghost" className="text-red-700 hover:bg-red-50 hover:text-red-700" onClick={() => setConfirmDelete(true)} disabled={busy}>
            <Trash2 aria-hidden="true" />
            Xóa báo giá
          </Button>
        </div>
      </aside>

      <ConfirmDialog
        open={confirmSend}
        onOpenChange={setConfirmSend}
        title="Gửi báo giá?"
        description="Bản nháp được lưu rồi gửi: số báo giá được cấp và nội dung bị khóa. Muốn sửa sau đó phải tạo version mới."
        confirmLabel="Gửi"
        pending={busy}
        onConfirm={saveAndSend}
      />
      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Xóa báo giá nháp?"
        description="Báo giá nháp sẽ bị xóa vĩnh viễn."
        confirmLabel="Xóa"
        destructive
        pending={remove.isPending}
        onConfirm={deleteDraft}
      />
    </div>
  )
}
