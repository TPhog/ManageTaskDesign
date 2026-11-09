import { Repeat } from 'lucide-react'
import { DateInput } from '@/components/DateInput'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Recurrence, RecurrenceKind } from '@/lib/types'
import { WEEKDAY_BITS, toggleWeekday } from './recurrenceUtils'

const NONE = 'NONE'
const kindLabel: Record<RecurrenceKind | typeof NONE, string> = {
  NONE: 'Không lặp',
  DAILY: 'Hằng ngày',
  WEEKLY: 'Hằng tuần',
  MONTHLY: 'Hằng tháng',
}

/** Weekday bit (Mon=1 … Sun=64) of a 'YYYY-MM-DD' date */
const weekdayBit = (iso: string) => 1 << ((new Date(`${iso}T00:00:00Z`).getUTCDay() + 6) % 7)

interface Props {
  value: Recurrence | null
  onChange: (value: Recurrence | null) => void
  /** Copy already spawned from this task (shown as plain text) */
  nextOccurrence?: { id: string; dueDate: string | null } | null
  /** Due date of the task: picks the first weekday / day of month when a rule is switched on */
  baseDate?: string | null
}

export function TaskRecurrenceField({ value, onChange, nextOccurrence, baseDate }: Props) {
  const kind = value?.kind ?? NONE

  function setKind(next: string) {
    if (next === NONE) return onChange(null)
    const base = baseDate || null
    onChange({
      kind: next as RecurrenceKind,
      weekdays: next === 'WEEKLY' ? (value?.weekdays || (base ? weekdayBit(base) : 1)) : 0,
      monthDay: next === 'MONTHLY' ? (value?.monthDay ?? (base ? Number(base.slice(8, 10)) : 1)) : null,
      until: value?.until ?? null,
    })
  }

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        <Label htmlFor="task-recurrence">Lặp lại</Label>
        <Select value={kind} onValueChange={setKind}>
          <SelectTrigger id="task-recurrence"><SelectValue /></SelectTrigger>
          <SelectContent>
            {([NONE, 'DAILY', 'WEEKLY', 'MONTHLY'] as const).map(k => <SelectItem key={k} value={k}>{kindLabel[k]}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      {value?.kind === 'WEEKLY' && (
        <div role="group" aria-label="Lặp vào các ngày" className="flex flex-wrap gap-1.5">
          {WEEKDAY_BITS.map(w => {
            const on = (value.weekdays & w.bit) !== 0
            return (
              <button
                key={w.bit}
                type="button"
                aria-pressed={on}
                aria-label={w.full}
                title={w.full}
                // at least one day stays selected (the API requires it)
                onClick={() => { const mask = toggleWeekday(value.weekdays, w.bit); if (mask) onChange({ ...value, weekdays: mask }) }}
                className={cn(
                  'inline-flex h-10 min-w-10 items-center justify-center rounded-md border px-2 text-sm font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  on ? 'border-primary bg-primary text-primary-foreground' : 'border-border-strong bg-card text-foreground hover:bg-surface-hover',
                )}
              >
                {w.short}
              </button>
            )
          })}
        </div>
      )}

      {value?.kind === 'MONTHLY' && (
        <div className="space-y-2">
          <Label htmlFor="task-recurrence-day">Ngày trong tháng</Label>
          <Input
            id="task-recurrence-day"
            type="number"
            min={1}
            max={31}
            className="w-28"
            aria-describedby="task-recurrence-day-hint"
            value={value.monthDay ?? ''}
            onChange={e => {
              const n = Number.parseInt(e.target.value, 10)
              onChange({ ...value, monthDay: Number.isNaN(n) ? null : Math.min(31, Math.max(1, n)) })
            }}
          />
          <p id="task-recurrence-day-hint" className="text-xs text-muted-foreground">Ngày 29–31 sẽ lùi về ngày cuối tháng nếu tháng đó ngắn hơn.</p>
        </div>
      )}

      {value && (
        <>
          <div className="space-y-2">
            <Label htmlFor="task-recurrence-until">Lặp đến ngày (tuỳ chọn)</Label>
            <DateInput id="task-recurrence-until" value={value.until ?? ''} onChange={v => onChange({ ...value, until: v || null })} />
          </div>
          <p className="flex items-start gap-1.5 text-xs text-muted-foreground">
            <Repeat className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            Khi bạn đánh dấu Xong, hệ thống tự tạo công việc kế tiếp cùng nội dung và checklist.
          </p>
        </>
      )}

      {nextOccurrence && (
        <p className="text-xs font-medium text-foreground">
          Đã tạo công việc kế tiếp ({nextOccurrence.dueDate ? `hạn ${formatDate(nextOccurrence.dueDate)}` : 'chưa có hạn'})
        </p>
      )}
    </div>
  )
}
