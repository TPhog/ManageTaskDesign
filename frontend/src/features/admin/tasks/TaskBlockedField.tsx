import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { taskStatusLabel } from './taskUtils'
import { useTaskList } from './useTasks'

// Radix Select reserves '' for "no value"
export const NO_BLOCKER = '_none'

interface Props {
  projectId: string
  taskId: string
  value: { reason: string; byTaskId: string | null }
  onChange: (value: { reason: string; byTaskId: string | null }) => void
}

/** Why a BLOCKED task waits, optionally on another task of the same project. Never changes a status itself (BR-01). */
export function TaskBlockedField({ projectId, taskId, value, onChange }: Props) {
  const { data } = useTaskList({ projectId })
  const candidates = (data?.items ?? []).filter(t => t.id !== taskId)
  const blocker = candidates.find(t => t.id === value.byTaskId)

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
        <Select value={value.byTaskId ?? NO_BLOCKER} onValueChange={v => onChange({ ...value, byTaskId: v === NO_BLOCKER ? null : v })}>
          <SelectTrigger id="task-blocked-by"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={NO_BLOCKER}>Không chọn</SelectItem>
            {candidates.map(t => (
              <SelectItem key={t.id} value={t.id}>
                {t.title}{t.status === 'DONE' ? ` (${taskStatusLabel.DONE})` : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      {blocker?.status === 'DONE' && (
        <p role="status" className="rounded-md bg-warning-soft px-3 py-2 text-xs text-foreground">
          Việc chặn đã xong — bạn có thể chuyển công việc này ra khỏi trạng thái Bị chặn.
        </p>
      )}
    </div>
  )
}
