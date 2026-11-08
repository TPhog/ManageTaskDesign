import { useCallback, useState, type FormEvent } from 'react'
import { ChevronDown, ChevronRight, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'
import type { ChecklistItem, TaskDetail } from '@/lib/types'
import { useChecklist } from './useTasks'
import { useDirtyMap, useReportDirty } from './useDirtyMap'

const DESCRIPTION_MAX = 5000

export function TaskChecklist({ task, onDirtyChange }: { task: TaskDetail; onDirtyChange?: (dirty: boolean) => void }) {
  const { add, update, remove } = useChecklist(task.id)
  const [text, setText] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)
  const report = useDirtyMap(onDirtyChange)
  const reportNew = useCallback((d: boolean) => report('new', d), [report])
  useReportDirty(text.trim() !== '', reportNew)
  const done = task.checklist.filter(i => i.done).length
  const total = task.checklist.length
  const pct = total === 0 ? 0 : Math.round((done * 100) / total)

  function submit(e: FormEvent) {
    e.preventDefault()
    const value = text.trim()
    if (!value) return
    add.mutate(value, { onSuccess: () => setText('') })
  }

  return (
    <section aria-labelledby="task-checklist-heading" className="space-y-3">
      <div className="flex items-center justify-between">
        <h3 id="task-checklist-heading" className="text-sm font-semibold text-foreground">Checklist</h3>
        <span className="text-xs tabular-nums text-muted-foreground">{done}/{total}</span>
      </div>
      {total > 0 && (
        <div
          role="progressbar"
          aria-label="Tiến độ checklist"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={pct}
          className="h-1.5 rounded-full bg-muted"
        >
          <div className="h-full rounded-full bg-primary transition-[width] duration-300" style={{ width: `${pct}%` }} />
        </div>
      )}
      <ul className="space-y-0.5">
        {task.checklist.map(item => (
          <ChecklistRow
            key={item.id}
            item={item}
            open={openId === item.id}
            saving={update.isPending}
            onDirtyChange={d => report(item.id, d)}
            onToggleOpen={() => setOpenId(openId === item.id ? null : item.id)}
            onToggleDone={checked => update.mutate({ id: item.id, text: item.text, done: checked })}
            onSaveDescription={description =>
              update.mutate(
                { id: item.id, text: item.text, done: item.done, description },
                { onSuccess: () => toast.success('Đã lưu mô tả') },
              )
            }
            onRemove={() => remove.mutate(item.id)}
          />
        ))}
      </ul>
      <form onSubmit={submit} className="flex gap-2">
        <Input
          aria-label="Thêm mục checklist"
          placeholder="Thêm mục…"
          value={text}
          maxLength={500}
          onChange={e => setText(e.target.value)}
        />
        <Button type="submit" variant="outline" disabled={add.isPending}>Thêm</Button>
      </form>
    </section>
  )
}

interface RowProps {
  item: ChecklistItem
  open: boolean
  saving: boolean
  onToggleOpen: () => void
  onToggleDone: (done: boolean) => void
  onSaveDescription: (description: string) => void
  onRemove: () => void
  onDirtyChange: (dirty: boolean) => void
}

/** Plan 06b F5: a subtask expands to show and edit its description. */
function ChecklistRow({ item, open, saving, onToggleOpen, onToggleDone, onSaveDescription, onRemove, onDirtyChange }: RowProps) {
  const [draft, setDraft] = useState(item.description)
  useReportDirty(draft !== item.description, onDirtyChange)

  return (
    <li className="rounded-md px-2 py-1.5 hover:bg-muted/60">
      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-7 w-7 shrink-0 text-muted-foreground"
          aria-label={`Mở mô tả ${item.text}`}
          aria-expanded={open}
          onClick={onToggleOpen}
        >
          {open ? <ChevronDown aria-hidden="true" /> : <ChevronRight aria-hidden="true" />}
        </Button>
        <input
          id={`checklist-${item.id}`}
          type="checkbox"
          checked={item.done}
          onChange={e => onToggleDone(e.target.checked)}
          className="h-4 w-4 shrink-0 accent-primary"
        />
        <label
          htmlFor={`checklist-${item.id}`}
          className={cn('flex-1 text-sm text-foreground', item.done && 'text-muted-foreground line-through')}
        >
          {item.text}
        </label>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-8 w-8 text-muted-foreground hover:text-red-700"
          aria-label={`Xoá mục ${item.text}`}
          onClick={onRemove}
        >
          <Trash2 aria-hidden="true" />
        </Button>
      </div>
      {!open && item.description && (
        <p className="ml-[3.75rem] mt-0.5 line-clamp-2 whitespace-pre-wrap text-xs text-muted-foreground">{item.description}</p>
      )}
      {open && (
        <div className="ml-9 mt-2 space-y-2">
          <label htmlFor={`checklist-desc-${item.id}`} className="sr-only">Mô tả của {item.text}</label>
          <Textarea
            id={`checklist-desc-${item.id}`}
            rows={3}
            maxLength={DESCRIPTION_MAX}
            placeholder="Chi tiết, link tham khảo…"
            value={draft}
            onChange={e => setDraft(e.target.value)}
          />
          <div className="flex justify-end">
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={saving || draft === item.description}
              onClick={() => onSaveDescription(draft)}
            >
              Lưu mô tả
            </Button>
          </div>
        </div>
      )}
    </li>
  )
}
