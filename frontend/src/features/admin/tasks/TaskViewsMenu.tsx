import { useState, type FormEvent } from 'react'
import { Bookmark, Check, Plus, RefreshCw, Trash2 } from 'lucide-react'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import type { SavedTaskView, TaskViewConfig } from '@/lib/types'
import { sameConfig } from './taskUtils'
import { useDeleteTaskView, useSaveTaskView, useTaskViews, useUpdateTaskView } from './useTaskViews'

const nameSchema = z.string().trim().min(1, 'Vui lòng nhập tên').max(60, 'Tối đa 60 ký tự')

const rowButton =
  'flex min-h-10 w-full items-center gap-2 rounded-md px-2 text-left text-sm transition-colors duration-150 hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

interface Props {
  current: TaskViewConfig
  onApply: (config: TaskViewConfig) => void
}

/** Saved task-list views: apply, save the current filters under a name, update or delete one. */
export function TaskViewsMenu({ current, onApply }: Props) {
  const { data, isLoading } = useTaskViews()
  const save = useSaveTaskView()
  const update = useUpdateTaskView()
  const remove = useDeleteTaskView()
  const [open, setOpen] = useState(false)
  const [appliedId, setAppliedId] = useState<string | null>(null)
  const [naming, setNaming] = useState(false)
  const [deleting, setDeleting] = useState<SavedTaskView | null>(null)

  const views = data?.views ?? []
  const matching = views.find(v => sameConfig(v.config, current))
  // the view picked last, now edited: offer to overwrite it
  const applied = views.find(v => v.id === appliedId)
  const changed = applied && !sameConfig(applied.config, current) ? applied : null

  function apply(view: SavedTaskView) {
    setAppliedId(view.id)
    onApply(view.config)
    setOpen(false)
  }

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button variant="outline" className="min-h-10 max-w-full">
            <Bookmark aria-hidden="true" />
            <span className="truncate">{matching ? `Chế độ xem: ${matching.name}` : 'Chế độ xem'}</span>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-2">
          <p className="px-2 pb-1 pt-1 text-kicker font-semibold uppercase tracking-[0.08em] text-muted-foreground">Chế độ xem đã lưu</p>
          {isLoading ? (
            <div className="space-y-1 p-2"><Skeleton className="h-8 w-full" /><Skeleton className="h-8 w-2/3" /></div>
          ) : views.length === 0 ? (
            <p className="px-2 py-2 text-sm text-muted-foreground">Chưa có chế độ xem nào. Chọn bộ lọc rồi lưu lại để dùng lần sau.</p>
          ) : (
            <ul className="m-0 max-h-72 list-none overflow-y-auto p-0">
              {views.map(v => {
                const active = matching?.id === v.id
                return (
                  <li key={v.id} className="flex items-center gap-1">
                    <button type="button" aria-current={active ? 'true' : undefined} onClick={() => apply(v)} className={cn(rowButton, active && 'font-semibold')}>
                      <Check className={cn('h-4 w-4 shrink-0', active ? 'text-foreground' : 'invisible')} aria-hidden="true" />
                      <span className="min-w-0 flex-1 truncate">{v.name}</span>
                    </button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="shrink-0 text-muted-foreground hover:text-destructive"
                      aria-label={`Xoá chế độ xem ${v.name}`}
                      onClick={() => { setOpen(false); setDeleting(v) }}
                    >
                      <Trash2 aria-hidden="true" />
                    </Button>
                  </li>
                )
              })}
            </ul>
          )}
          <div className="mt-1 border-t pt-1">
            {changed && (
              <button
                type="button"
                className={rowButton}
                disabled={update.isPending}
                onClick={() => update.mutate({ id: changed.id, name: changed.name, config: current }, { onSuccess: () => setOpen(false) })}
              >
                <RefreshCw className="h-4 w-4 shrink-0" aria-hidden="true" />
                <span className="min-w-0 truncate">Cập nhật “{changed.name}”</span>
              </button>
            )}
            <button type="button" className={rowButton} onClick={() => { setOpen(false); setNaming(true) }}>
              <Plus className="h-4 w-4 shrink-0" aria-hidden="true" />
              Lưu chế độ xem hiện tại…
            </button>
          </div>
        </PopoverContent>
      </Popover>

      {/* mounted only while open, so every opening starts with an empty name */}
      {naming && (
        <SaveViewDialog
          onOpenChange={setNaming}
          pending={save.isPending}
          onSave={name => save.mutate({ name, config: current }, {
            onSuccess: view => { setAppliedId(view.id); setNaming(false) },
          })}
        />
      )}

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={o => { if (!o) setDeleting(null) }}
        title="Xoá chế độ xem?"
        description={deleting ? `“${deleting.name}” sẽ bị xoá. Bộ lọc đang dùng không đổi.` : ''}
        confirmLabel="Xoá"
        destructive
        pending={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })}
      />
    </>
  )
}

function SaveViewDialog({ onOpenChange, pending, onSave }: {
  onOpenChange: (open: boolean) => void
  pending: boolean
  onSave: (name: string) => void
}) {
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  function submit(e: FormEvent) {
    e.preventDefault()
    const parsed = nameSchema.safeParse(name)
    if (!parsed.success) { setError(parsed.error.issues[0].message); return }
    setError(null)
    onSave(parsed.data)
  }

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Lưu chế độ xem</DialogTitle>
          <DialogDescription>Lưu bộ lọc, cách nhóm và kiểu xem (danh sách hoặc lịch) hiện tại để mở lại bằng một cú bấm.</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} noValidate className="space-y-2">
          <Label htmlFor="task-view-name">Tên chế độ xem</Label>
          <Input
            id="task-view-name"
            value={name}
            maxLength={80}
            placeholder="Ví dụ: Việc gấp tuần này"
            aria-invalid={!!error}
            aria-describedby={error ? 'task-view-name-error' : undefined}
            onChange={e => setName(e.target.value)}
          />
          {error && <p id="task-view-name-error" className="text-sm text-destructive">{error}</p>}
          <DialogFooter className="gap-2 pt-3">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Huỷ</Button>
            <Button type="submit" disabled={pending}>{pending ? 'Đang lưu…' : 'Lưu'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
