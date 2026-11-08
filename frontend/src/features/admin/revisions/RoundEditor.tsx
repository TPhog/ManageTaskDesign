import { Plus, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { BillingChip } from '@/components/BillingChip'
import { billingLabel, itemTypeLabel } from '@/lib/revisionLabels'
import type { Billing, RevisionItemInput, RevisionItemType, RoundInput } from '@/lib/revisionTypes'
import { emptyItem } from './revisionUtils'
import { useSuggestBilling } from './useRevisions'

// Radix Select reserves '' for "no value"
const PROJECT_LEVEL = '_project'
const AUTO = '_auto'

export interface RoundDeliverableOption {
  id: string
  name: string
  usedRounds: number
  includedRounds: number
}

interface RoundEditorProps {
  index: number
  value: RoundInput
  deliverables: RoundDeliverableOption[]
  onChange: (round: RoundInput) => void
  onRemove?: () => void
}

/** One round being composed (docs/08 §2.3): deliverable, received date, items with the server's billing suggestion. */
export function RoundEditor({ index, value, deliverables, onChange, onRemove }: RoundEditorProps) {
  const n = index + 1
  const { data: suggestions } = useSuggestBilling(value.deliverableId, value.items.map(i => i.type))

  const setItem = (at: number, patch: Partial<RevisionItemInput>) =>
    onChange({ ...value, items: value.items.map((item, i) => (i === at ? { ...item, ...patch } : item)) })

  function setDeliverable(id: string) {
    const deliverableId = id === PROJECT_LEVEL ? null : id
    // BR-41: a project-level round cannot keep IN_QUOTA items
    const items = deliverableId ? value.items : value.items.map(i => (i.billing === 'IN_QUOTA' ? { ...i, billing: null } : i))
    onChange({ ...value, deliverableId, items })
  }

  return (
    <fieldset className="space-y-3 rounded-lg border p-4">
      <legend className="px-1 text-sm font-semibold text-foreground">Vòng {n}</legend>
      <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
        <div className="space-y-1.5">
          <Label htmlFor={`round-${n}-deliverable`}>Hạng mục vòng {n}</Label>
          <Select value={value.deliverableId ?? PROJECT_LEVEL} onValueChange={setDeliverable}>
            <SelectTrigger id={`round-${n}-deliverable`}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {deliverables.map(d => (
                <SelectItem key={d.id} value={d.id}>{d.name} ({d.usedRounds}/{d.includedRounds} vòng)</SelectItem>
              ))}
              <SelectItem value={PROJECT_LEVEL}>Cấp dự án (không dùng quota)</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor={`round-${n}-received`}>Ngày nhận</Label>
          <Input
            id={`round-${n}-received`}
            type="date"
            value={value.receivedAt ?? ''}
            onChange={e => onChange({ ...value, receivedAt: e.target.value || null })}
          />
        </div>
      </div>

      <ul className="space-y-3">
        {value.items.map((item, i) => {
          const m = i + 1
          const suggestion = suggestions?.[i]
          return (
            <li key={i} className="space-y-2 rounded-md bg-muted/40 p-3">
              <div className="flex items-start gap-2">
                <Input
                  aria-label={`Mô tả mục ${m}`}
                  placeholder="Mô tả chỉnh sửa"
                  maxLength={2000}
                  value={item.description}
                  onChange={e => setItem(i, { description: e.target.value })}
                />
                {value.items.length > 1 && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={`Xoá mục ${m}`}
                    onClick={() => onChange({ ...value, items: value.items.filter((_, k) => k !== i) })}
                  >
                    <Trash2 aria-hidden="true" />
                  </Button>
                )}
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Select value={item.type} onValueChange={v => setItem(i, { type: v as RevisionItemType })}>
                  <SelectTrigger aria-label={`Loại mục ${m}`} className="w-40">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(Object.keys(itemTypeLabel) as RevisionItemType[]).map(t => (
                      <SelectItem key={t} value={t}>{itemTypeLabel[t]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select value={item.billing ?? AUTO} onValueChange={v => setItem(i, { billing: v === AUTO ? null : (v as Billing) })}>
                  <SelectTrigger aria-label={`Tính phí mục ${m}`} className="w-48">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={AUTO}>Theo gợi ý</SelectItem>
                    {(Object.keys(billingLabel) as Billing[]).map(b => (
                      <SelectItem key={b} value={b} disabled={b === 'IN_QUOTA' && !value.deliverableId}>{billingLabel[b]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {suggestion && (
                  <span className="flex min-w-0 flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                    Gợi ý: <BillingChip billing={suggestion.billing} />
                    <span>{suggestion.reason}</span>
                  </span>
                )}
              </div>
            </li>
          )
        })}
      </ul>

      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" size="sm" onClick={() => onChange({ ...value, items: [...value.items, emptyItem()] })}>
          <Plus aria-hidden="true" />
          Thêm mục
        </Button>
        {onRemove && (
          <Button type="button" variant="ghost" size="sm" onClick={onRemove}>Bỏ vòng này</Button>
        )}
      </div>
    </fieldset>
  )
}
