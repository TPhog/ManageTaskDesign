import { useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { TaskStatus } from '@/lib/types'
import { useCreateTask } from './useTasks'

interface Props {
  projectId: string
  /** Column the task is created in; the API defaults to TODO */
  status?: TaskStatus
  label: string
}

/** Jira-style inline create: type a title, Enter saves and keeps the box open, Esc closes. */
export function QuickCreateTask({ projectId, status, label }: Props) {
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const create = useCreateTask(projectId)

  function submit(e: FormEvent) {
    e.preventDefault()
    const value = title.trim()
    if (!value) return
    create.mutate({ title: value, status }, { onSuccess: () => setTitle('') })
  }

  function close() {
    setOpen(false)
    setTitle('')
  }

  if (!open) {
    return (
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="w-full justify-start text-muted-foreground"
        onClick={() => setOpen(true)}
      >
        <Plus aria-hidden="true" />
        {label}
      </Button>
    )
  }

  return (
    <form onSubmit={submit}>
      <Input
        autoFocus
        aria-label={label}
        placeholder="Nhập tiêu đề rồi nhấn Enter"
        value={title}
        maxLength={200}
        onChange={e => setTitle(e.target.value)}
        onKeyDown={e => { if (e.key === 'Escape') close() }}
        onBlur={() => { if (!title.trim()) close() }}
      />
    </form>
  )
}
