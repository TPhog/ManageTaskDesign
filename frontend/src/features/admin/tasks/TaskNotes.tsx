import { useState, type FormEvent } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { formatDatetime } from '@/lib/format'
import type { TaskNote } from '@/lib/types'
import { useTaskNoteMutations, useTaskNotes } from './useTasks'

export const NOTE_MAX_LENGTH = 5000

/** Plan 06b F5: timestamped notes, newest first, designer-only. */
export function TaskNotes({ taskId }: { taskId: string }) {
  const { data: notes, isLoading, isError } = useTaskNotes(taskId)
  const { add, update, remove } = useTaskNoteMutations(taskId)
  const [text, setText] = useState('')

  function submit(e: FormEvent) {
    e.preventDefault()
    const body = text.trim()
    if (!body) return
    add.mutate(body, { onSuccess: () => setText('') })
  }

  return (
    <section aria-labelledby="task-notes-heading" className="space-y-3">
      <div>
        <h3 id="task-notes-heading" className="text-sm font-semibold text-foreground">Ghi chú ({notes?.length ?? 0})</h3>
        <p className="text-xs text-muted-foreground">Chỉ bạn thấy — khách không xem được.</p>
      </div>
      <form onSubmit={submit} className="space-y-2">
        <Label htmlFor={`task-note-new-${taskId}`} className="sr-only">Ghi chú mới</Label>
        <Textarea
          id={`task-note-new-${taskId}`}
          rows={3}
          maxLength={NOTE_MAX_LENGTH}
          placeholder="Ghi lại trao đổi, quyết định, link…"
          value={text}
          onChange={e => setText(e.target.value)}
        />
        <div className="flex justify-end">
          <Button type="submit" size="sm" disabled={!text.trim() || add.isPending}>
            {add.isPending ? 'Đang lưu…' : 'Thêm ghi chú'}
          </Button>
        </div>
      </form>
      {isLoading ? (
        <Skeleton className="h-16 w-full" />
      ) : isError ? (
        <p className="text-sm text-muted-foreground">Không tải được ghi chú.</p>
      ) : notes && notes.length > 0 ? (
        <ul aria-label="Danh sách ghi chú" className="space-y-2">
          {notes.map(note => (
            <NoteItem
              key={note.id}
              note={note}
              saving={update.isPending}
              deleting={remove.isPending}
              onSave={(body, done) => update.mutate({ id: note.id, body }, { onSuccess: done })}
              onDelete={() => remove.mutate(note.id)}
            />
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground">Chưa có ghi chú nào.</p>
      )}
    </section>
  )
}

interface NoteItemProps {
  note: TaskNote
  saving: boolean
  deleting: boolean
  onSave: (body: string, done: () => void) => void
  onDelete: () => void
}

function NoteItem({ note, saving, deleting, onSave, onDelete }: NoteItemProps) {
  const [editing, setEditing] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [draft, setDraft] = useState(note.body)
  const edited = note.updatedAt !== note.createdAt

  return (
    <li className="rounded-lg border p-3">
      <div className="flex items-center justify-between gap-2">
        <time dateTime={note.createdAt} className="text-xs tabular-nums text-muted-foreground">
          {formatDatetime(note.createdAt)}
          {edited && ' (đã sửa)'}
        </time>
        {!editing && (
          <div className="flex items-center">
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground"
              aria-label="Sửa ghi chú"
              onClick={() => { setDraft(note.body); setEditing(true) }}
            >
              <Pencil aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:text-red-700"
              aria-label="Xoá ghi chú"
              onClick={() => setConfirming(true)}
            >
              <Trash2 aria-hidden="true" />
            </Button>
          </div>
        )}
      </div>
      {editing ? (
        <div className="mt-2 space-y-2">
          <Label htmlFor={`note-edit-${note.id}`} className="sr-only">Nội dung ghi chú</Label>
          <Textarea
            id={`note-edit-${note.id}`}
            rows={3}
            maxLength={NOTE_MAX_LENGTH}
            value={draft}
            onChange={e => setDraft(e.target.value)}
          />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={() => setEditing(false)}>Huỷ</Button>
            <Button
              type="button"
              size="sm"
              disabled={saving || !draft.trim()}
              onClick={() => onSave(draft.trim(), () => setEditing(false))}
            >
              Lưu
            </Button>
          </div>
        </div>
      ) : (
        <p className="mt-1 whitespace-pre-wrap break-words text-sm text-foreground">{note.body}</p>
      )}
      {confirming && (
        <div className="mt-2 flex items-center gap-2">
          <Button type="button" variant="destructive" size="sm" disabled={deleting} onClick={onDelete}>Xác nhận xoá</Button>
          <Button type="button" variant="ghost" size="sm" onClick={() => setConfirming(false)}>Huỷ</Button>
        </div>
      )}
    </li>
  )
}
