import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { MarkdownField } from './MarkdownField'
import { Skeleton } from '@/components/ui/skeleton'
import { DateInput } from '@/components/DateInput'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { z } from 'zod'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { ProjectDetail, Recurrence, RecurrenceInput, TaskDetail } from '@/lib/types'
import { TaskChecklist } from './TaskChecklist'
import { LinkListEditor } from './LinkListEditor'
import { NO_BLOCKER, TaskBlockedField } from './TaskBlockedField'
import { TaskRecurrenceField } from './TaskRecurrenceField'
import { useTaskImages } from '@/features/admin/images/useImages'
import { TaskImages } from './TaskImages'
import { TaskNotes } from './TaskNotes'
import { TASK_PRIORITIES, TASK_STATUSES, taskFormSchema, taskPriorityLabel, taskStatusLabel } from './taskUtils'
import { useDirtyMap, useReportDirty } from './useDirtyMap'
import { useDeleteTask, useTask, useUpdateTask } from './useTasks'

// Radix Select reserves '' for "no value"
const NO_DELIVERABLE = '_none'

// P15: repeat rule, external links and the blocked reason ride along with the main task form
const schema = taskFormSchema.and(z.object({
  links: z.array(z.object({ title: z.string(), url: z.string(), visibleToClient: z.boolean() })).max(10),
  recurrence: z.object({
    kind: z.enum(['NONE', 'DAILY', 'WEEKLY', 'MONTHLY']),
    weekdays: z.number(),
    monthDay: z.number().nullable(),
    until: z.string(),
  }),
  blockedReason: z.string().max(500, 'Tối đa 500 ký tự'),
  blockedByTaskId: z.string(),
}))

type FormValues = z.infer<typeof schema>

const NO_RECURRENCE: FormValues['recurrence'] = { kind: 'NONE', weekdays: 0, monthDay: null, until: '' }
const toFormRecurrence = (r: Recurrence | null): FormValues['recurrence'] => (r ? { ...r, until: r.until ?? '' } : NO_RECURRENCE)
const fromFormRecurrence = (r: FormValues['recurrence']): Recurrence | null =>
  r.kind === 'NONE' ? null : { kind: r.kind, weekdays: r.weekdays, monthDay: r.monthDay, until: r.until || null }

interface Props {
  /** null = closed */
  taskId: string | null
  onClose: () => void
}

export function TaskDrawer({ taskId, onClose }: Props) {
  const { data: task, isLoading, isError } = useTask(taskId)
  const [dirty, setDirty] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const report = useDirtyMap(setDirty)
  const reportForm = useCallback((d: boolean) => report('form', d), [report])
  const reportChecklist = useCallback((d: boolean) => report('checklist', d), [report])
  const reportNotes = useCallback((d: boolean) => report('notes', d), [report])

  // D-30: leaving the page with unsaved edits asks the browser to confirm
  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = '' }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  // Esc, overlay click and the X button all arrive here
  function requestClose() {
    if (dirty) setConfirmDiscard(true)
    else onClose()
  }

  return (
    <>
    <Sheet open={!!taskId} onOpenChange={open => { if (!open) requestClose() }}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
        <SheetHeader className="pr-8">
          <SheetTitle>Chi tiết công việc</SheetTitle>
          <SheetDescription>
            {task ? (
              <Link to={`/admin/projects/${task.projectId}?tab=tasks`} className="hover:text-foreground hover:underline">
                {task.projectName}
              </Link>
            ) : (
              'Đang tải…'
            )}
          </SheetDescription>
        </SheetHeader>

        {isLoading ? (
          <div className="mt-6 space-y-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-10 w-2/3" />
          </div>
        ) : isError || !task ? (
          <p className="mt-6 text-sm text-muted-foreground">Không tải được công việc — có thể nó đã bị xoá.</p>
        ) : (
          <div className="mt-6 space-y-8">
            {/* remount after each save so the form shows the server's values */}
            <TaskForm key={`${task.id}-${task.updatedAt}`} task={task} onDeleted={onClose} onDirtyChange={reportForm} />
            <TaskChecklist task={task} onDirtyChange={reportChecklist} />
            <TaskImages taskId={task.id} />
            <TaskNotes taskId={task.id} onDirtyChange={reportNotes} />
          </div>
        )}
      </SheetContent>
    </Sheet>
    <ConfirmDialog
      open={confirmDiscard}
      onOpenChange={setConfirmDiscard}
      title="Bỏ thay đổi chưa lưu?"
      description="Các thay đổi chưa lưu trong công việc này sẽ mất."
      confirmLabel="Bỏ thay đổi"
      cancelLabel="Tiếp tục sửa"
      destructive
      onConfirm={() => { setConfirmDiscard(false); onClose() }}
    />
    </>
  )
}

function TaskForm({ task, onDeleted, onDirtyChange }: { task: TaskDetail; onDeleted: () => void; onDirtyChange: (dirty: boolean) => void }) {
  const update = useUpdateTask(task.id)
  const remove = useDeleteTask()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const imageCount = useTaskImages(task.id).data?.length ?? 0

  const { data: project } = useQuery({
    queryKey: qk.projects.detail(task.projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${task.projectId}`),
  })
  const deliverables = project?.deliverables ?? []

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { register, handleSubmit, watch, setValue, setError, trigger, formState: { errors, isDirty } } = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      title: task.title,
      description: task.description,
      status: task.status,
      priority: task.priority,
      startDate: task.startDate ?? '',
      dueDate: task.dueDate ?? '',
      deliverableId: task.deliverableId ?? NO_DELIVERABLE,
      links: task.links ?? [],
      recurrence: toFormRecurrence(task.recurrence ?? null),
      blockedReason: task.blocked?.reason ?? '',
      blockedByTaskId: task.blocked?.byTaskId ?? NO_BLOCKER,
    },
  })
  // server-side errors of the P15 fields (400 errors.recurrence / links / blocked.*)
  const [extraErrors, setExtraErrors] = useState<Record<string, string>>({})

  useReportDirty(isDirty, onDirtyChange)

  function onSubmit(v: FormValues) {
    const rule = fromFormRecurrence(v.recurrence)
    const recurrence: RecurrenceInput = rule ?? { kind: 'NONE', weekdays: 0, monthDay: null, until: null }
    setExtraErrors({})
    update.mutate({
      title: v.title,
      description: v.description,
      status: v.status,
      priority: v.priority,
      startDate: v.startDate || null,
      dueDate: v.dueDate || null,
      deliverableId: v.deliverableId === NO_DELIVERABLE ? null : v.deliverableId,
      links: v.links,
      recurrence,
      blocked: v.status === 'BLOCKED'
        ? { reason: v.blockedReason.trim() || null, byTaskId: v.blockedByTaskId === NO_BLOCKER ? null : v.blockedByTaskId }
        : undefined,
    }, {
      // the server enforces the same rule (400 errors.dueDate): show it on the field too
      onError: err => {
        const errors = (err as ApiError).errors ?? {}
        const message = errors.dueDate?.[0]
        if (message) setError('dueDate', { message })
        const pick = (prefix: string) => Object.entries(errors).find(([k]) => k === prefix || k.startsWith(`${prefix}.`) || k.startsWith(`${prefix}[`))?.[1]?.[0]
        setExtraErrors(Object.fromEntries(
          (['recurrence', 'links', 'blocked'] as const).flatMap(k => { const m = pick(k); return m ? [[k, m]] : [] }),
        ))
      },
    })
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
      <div className="space-y-2">
        <Label htmlFor="task-title">Tiêu đề</Label>
        <Input
          id="task-title"
          aria-invalid={!!errors.title}
          aria-describedby={errors.title ? 'task-title-error' : undefined}
          {...register('title')}
        />
        {errors.title && <p id="task-title-error" className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="task-status">Trạng thái</Label>
          <Select
            value={watch('status')}
            onValueChange={v => setValue('status', v as FormValues['status'], { shouldDirty: true })}
          >
            <SelectTrigger id="task-status"><SelectValue /></SelectTrigger>
            <SelectContent>
              {TASK_STATUSES.map(s => <SelectItem key={s} value={s}>{taskStatusLabel[s]}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="task-priority">Ưu tiên</Label>
          <Select
            value={watch('priority')}
            onValueChange={v => setValue('priority', v as FormValues['priority'], { shouldDirty: true })}
          >
            <SelectTrigger id="task-priority"><SelectValue /></SelectTrigger>
            <SelectContent>
              {TASK_PRIORITIES.map(p => <SelectItem key={p} value={p}>{taskPriorityLabel[p]}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="task-startDate">Ngày bắt đầu</Label>
          <DateInput id="task-startDate" value={watch('startDate')} onChange={v => { setValue('startDate', v, { shouldDirty: true }); if (watch('dueDate')) void trigger('dueDate') }} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="task-dueDate">Hạn hoàn thành</Label>
          <DateInput id="task-dueDate" value={watch('dueDate')} onChange={v => setValue('dueDate', v, { shouldDirty: true, shouldValidate: !!errors.dueDate })} />
          {errors.dueDate && <p id="task-dueDate-error" role="alert" className="text-sm text-destructive">{errors.dueDate.message}</p>}
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="task-deliverable">Hạng mục</Label>
        <Select
          value={watch('deliverableId')}
          onValueChange={v => setValue('deliverableId', v, { shouldDirty: true })}
        >
          <SelectTrigger id="task-deliverable"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={NO_DELIVERABLE}>Không gắn hạng mục</SelectItem>
            {deliverables.map(d => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      {watch('status') === 'BLOCKED' && (
        <div className="space-y-1.5">
          <TaskBlockedField
            projectId={task.projectId}
            taskId={task.id}
            value={{ reason: watch('blockedReason'), byTaskId: watch('blockedByTaskId') === NO_BLOCKER ? null : watch('blockedByTaskId') }}
            onChange={b => {
              setValue('blockedReason', b.reason, { shouldDirty: true })
              setValue('blockedByTaskId', b.byTaskId ?? NO_BLOCKER, { shouldDirty: true })
            }}
            saved={task.blocked}
          />
          {extraErrors.blocked && <p role="alert" className="text-sm text-destructive">{extraErrors.blocked}</p>}
        </div>
      )}

      <div className="space-y-1.5">
        <TaskRecurrenceField
          value={fromFormRecurrence(watch('recurrence'))}
          onChange={r => setValue('recurrence', toFormRecurrence(r), { shouldDirty: true })}
          nextOccurrence={task.nextOccurrence}
          baseDate={watch('dueDate') || null}
        />
        {extraErrors.recurrence && <p role="alert" className="text-sm text-destructive">{extraErrors.recurrence}</p>}
      </div>

      <div className="space-y-2">
        <Label asChild><span>Liên kết ngoài</span></Label>
        <LinkListEditor value={watch('links')} onChange={v => setValue('links', v, { shouldDirty: true })} />
        {extraErrors.links && <p role="alert" className="text-sm text-destructive">{extraErrors.links}</p>}
      </div>

      <MarkdownField
        id="task-description"
        label="Mô tả"
        value={watch('description')}
        onChange={v => setValue('description', v, { shouldDirty: true })}
        maxLength={20000}
        placeholder="Mục tiêu, link tham khảo, yêu cầu của khách… (Markdown)"
        error={errors.description?.message}
      />

      <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
        {confirmDelete ? (
          <div className="space-y-1.5">
            {imageCount > 0 && (
              <p id="task-delete-images-warning" className="text-xs text-muted-foreground">
                {`${imageCount} ảnh của công việc cũng bị xoá vĩnh viễn (trừ ảnh là chứng từ báo giá).`}
              </p>
            )}
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="destructive"
                size="sm"
                disabled={remove.isPending}
                aria-describedby={imageCount > 0 ? 'task-delete-images-warning' : undefined}
                onClick={() => remove.mutate(task.id, { onSuccess: onDeleted })}
              >
                Xác nhận xoá
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setConfirmDelete(false)}>Huỷ</Button>
            </div>
          </div>
        ) : (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="text-red-700 hover:bg-red-50 hover:text-red-700"
            onClick={() => setConfirmDelete(true)}
          >
            <Trash2 aria-hidden="true" />
            Xoá công việc
          </Button>
        )}
        <Button type="submit" disabled={!isDirty || update.isPending}>
          {update.isPending ? 'Đang lưu…' : 'Lưu thay đổi'}
        </Button>
      </div>
    </form>
  )
}
