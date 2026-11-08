import { useState, type FormEvent } from 'react'
import { Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import type { TaskDetail } from '@/lib/types'
import { useChecklist } from './useTasks'

export function TaskChecklist({ task }: { task: TaskDetail }) {
  const { add, update, remove } = useChecklist(task.id)
  const [text, setText] = useState('')
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
          <li key={item.id} className="flex items-center gap-2.5 rounded-md px-2 py-1.5 hover:bg-muted/60">
            <input
              id={`checklist-${item.id}`}
              type="checkbox"
              checked={item.done}
              onChange={e => update.mutate({ id: item.id, text: item.text, done: e.target.checked })}
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
              onClick={() => remove.mutate(item.id)}
            >
              <Trash2 aria-hidden="true" />
            </Button>
          </li>
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
