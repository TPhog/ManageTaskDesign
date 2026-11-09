import { useState } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SearchCombobox, type ComboOption } from '@/components/SearchCombobox'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { taskStatusLabel } from './taskUtils'
import { useTaskList } from './useTasks'

// kept for the drawer form, which stores "no blocker" as this value
export const NO_BLOCKER = '_none'

/** Matches shown per search; the list is searched on the server, so any task of the project can be picked */
const PAGE = 20

interface Props {
  projectId: string
  taskId: string
  value: { reason: string; byTaskId: string | null }
  onChange: (value: { reason: string; byTaskId: string | null }) => void
  /** The saved blocker (from task.blocked): shown even when it is not among the loaded matches */
  saved?: { byTaskId: string | null; byTaskTitle: string | null; byTaskDone: boolean } | null
}

/** Why a BLOCKED task waits, optionally on another task of the same project. Never changes a status itself (BR-01). */
export function TaskBlockedField({ projectId, taskId, value, onChange, saved }: Props) {
  const [search, setSearch] = useState('')
  const q = useDebouncedValue(search.trim())
  const { data, isLoading } = useTaskList({ projectId, q: q || undefined, pageSize: PAGE })
  const candidates = (data?.items ?? []).filter(t => t.id !== taskId)
  const options: ComboOption[] = candidates.map(t => ({
    value: t.id,
    label: t.title,
    hint: t.status === 'DONE' ? taskStatusLabel.DONE : undefined,
  }))

  // label + done flag of the current pick: a loaded match, the one just chosen, or the saved blocker
  const [picked, setPicked] = useState<{ id: string; title: string; done: boolean } | null>(
    saved?.byTaskId ? { id: saved.byTaskId, title: saved.byTaskTitle ?? '', done: saved.byTaskDone } : null,
  )
  const loaded = candidates.find(t => t.id === value.byTaskId)
  const current = loaded
    ? { title: loaded.title, done: loaded.status === 'DONE' }
    : picked && picked.id === value.byTaskId ? picked : null

  return (
    <div className="space-y-3 rounded-lg border border-border-strong bg-surface-sunk p-3">
      <div className="space-y-2">
        <Label htmlFor="task-blocked-reason">Lý do bị chặn</Label>
        <Input
          id="task-blocked-reason"
          maxLength={500}
          placeholder="Ví dụ: Chờ khách gửi logo"
          value={value.reason}
          onChange={e => onChange({ ...value, reason: e.target.value })}
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="task-blocked-by">Bị chặn bởi công việc</Label>
        <SearchCombobox
          id="task-blocked-by"
          value={value.byTaskId ?? ''}
          selectedLabel={current ? `${current.title}${current.done ? ` (${taskStatusLabel.DONE})` : ''}` : undefined}
          options={options}
          loading={isLoading}
          search={search}
          onSearchChange={setSearch}
          onSelect={o => {
            const task = o ? candidates.find(t => t.id === o.value) : undefined
            setPicked(task ? { id: task.id, title: task.title, done: task.status === 'DONE' } : null)
            onChange({ ...value, byTaskId: o?.value ?? null })
          }}
          placeholder="Không chọn"
          clearLabel="Không chọn"
          searchPlaceholder="Tìm công việc trong dự án…"
          emptyText="Không có công việc khớp"
        />
      </div>
      {current?.done && (
        <p role="status" className="rounded-md bg-warning-soft px-3 py-2 text-xs text-foreground">
          Việc chặn đã xong — bạn có thể chuyển công việc này ra khỏi trạng thái Bị chặn.
        </p>
      )}
    </div>
  )
}
