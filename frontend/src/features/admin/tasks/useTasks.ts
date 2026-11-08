import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type {
  PagedResult,
  TaskDetail,
  TaskListItem,
  TaskListParams,
  TaskNote,
  TaskStatus,
  TaskUpdateInput,
} from '@/lib/types'

function toQuery(params: TaskListParams): string {
  // pageSize is capped at 100 by the API (docs/05 §1.3); the Kanban of one project loads 100
  const search = new URLSearchParams({ pageSize: String(params.pageSize ?? 100) })
  if (params.page) search.set('page', String(params.page))
  if (params.projectId) search.set('projectId', params.projectId)
  if (params.status) search.set('status', params.status)
  if (params.due) search.set('due', params.due)
  if (params.q) search.set('q', params.q)
  if (params.clientId) search.set('clientId', params.clientId)
  return search.toString()
}

function errorText(err: ApiError, fallback: string): string {
  const fieldError = err.errors ? Object.values(err.errors).flat()[0] : undefined
  return fieldError ?? err.detail ?? fallback
}

/** Every task write returns the fresh TaskDetail: store it, then refresh the lists that show the task. */
function onTaskSaved(qc: QueryClient, task: TaskDetail | undefined) {
  if (task) qc.setQueryData(qk.tasks.detail(task.id), task)
  // plan 06b: task status feeds deliverable progress and the project flow (both under qk.projects.all)
  return Promise.all([
    qc.invalidateQueries({ queryKey: qk.tasks.lists }),
    qc.invalidateQueries({ queryKey: qk.projects.all }),
    qc.invalidateQueries({ queryKey: qk.dashboard }),
  ])
}

export function useTaskList(params: TaskListParams) {
  return useQuery({
    queryKey: qk.tasks.list(params),
    queryFn: () => api.get<PagedResult<TaskListItem>>(`/admin/tasks?${toQuery(params)}`),
  })
}

/** QA D-18: how many tasks a quick filter holds, without loading them (pageSize=1). */
export function useTaskCount(params: TaskListParams) {
  return useQuery({
    queryKey: [...qk.tasks.lists, 'count', params],
    queryFn: () => api.get<PagedResult<TaskListItem>>(`/admin/tasks?${toQuery({ ...params, page: 1, pageSize: 1 })}`),
    select: d => d.total,
  })
}

export function useTask(id: string | null) {
  return useQuery({
    queryKey: qk.tasks.detail(id ?? ''),
    queryFn: () => api.get<TaskDetail>(`/admin/tasks/${id}`),
    enabled: !!id,
  })
}

export function useCreateTask(projectId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { title: string; status?: TaskStatus }) =>
      api.post<TaskDetail>(`/admin/projects/${projectId}/tasks`, body),
    onSuccess: task => onTaskSaved(qc, task),
    onError: (err: ApiError) => toast.error(errorText(err, 'Không thể tạo công việc')),
  })
}

export function useUpdateTask(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: TaskUpdateInput) => api.put<TaskDetail>(`/admin/tasks/${id}`, body),
    onSuccess: task => {
      toast.success('Đã lưu công việc')
      return onTaskSaved(qc, task)
    },
    onError: (err: ApiError) => toast.error(errorText(err, 'Không thể lưu công việc')),
  })
}

export function useMoveTask() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status, sortOrder }: { id: string; status: TaskStatus; sortOrder: number }) =>
      api.post<TaskDetail>(`/admin/tasks/${id}/move`, { status, sortOrder }),
    // Optimistic: the card stays where it was dropped while the request runs
    onMutate: async ({ id, status, sortOrder }) => {
      await qc.cancelQueries({ queryKey: qk.tasks.lists })
      const snapshots = qc.getQueriesData<PagedResult<TaskListItem>>({ queryKey: qk.tasks.lists })
      for (const [key, data] of snapshots) {
        if (!data) continue
        qc.setQueryData<PagedResult<TaskListItem>>(key, {
          ...data,
          items: data.items.map(t => (t.id === id ? { ...t, status, sortOrder } : t)),
        })
      }
      return { snapshots }
    },
    onError: (err: ApiError, _vars, ctx) => {
      for (const [key, data] of ctx?.snapshots ?? []) qc.setQueryData(key, data)
      toast.error(errorText(err, 'Không thể di chuyển công việc'))
    },
    onSuccess: task => {
      if (task) qc.setQueryData(qk.tasks.detail(task.id), task)
    },
    // Refetch the lists either way so the cache never keeps a card in a position the server rejected
    onSettled: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: qk.tasks.lists }),
        qc.invalidateQueries({ queryKey: qk.projects.all }),
        qc.invalidateQueries({ queryKey: qk.dashboard }),
      ]),
  })
}

export function useDeleteTask() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/tasks/${id}`),
    // The detail cache entry is left alone: removing it while the drawer still observes it triggers a 404 refetch.
    // The drawer closes right after (onDeleted) and nothing can reach the deleted id again.
    onSuccess: () => {
      toast.success('Đã xoá công việc')
      // task counts drive project progress and the SUBMIT_REVIEW flow action
      qc.invalidateQueries({ queryKey: qk.projects.all })
      qc.invalidateQueries({ queryKey: qk.dashboard })
      return qc.invalidateQueries({ queryKey: qk.tasks.lists })
    },
    onError: (err: ApiError) => toast.error(errorText(err, 'Không thể xoá công việc')),
  })
}

export function useChecklist(taskId: string) {
  const qc = useQueryClient()
  const detailKey = qk.tasks.detail(taskId)
  const onSuccess = (task: TaskDetail) => onTaskSaved(qc, task)
  const onError = (err: ApiError) => toast.error(errorText(err, 'Không thể cập nhật checklist'))

  const add = useMutation({
    mutationFn: (text: string) => api.post<TaskDetail>(`/admin/tasks/${taskId}/checklist`, { text }),
    onSuccess,
    onError,
  })

  const update = useMutation({
    // description omitted = the server keeps it (the checkbox toggle does not send it)
    mutationFn: (item: { id: string; text: string; done: boolean; description?: string }) =>
      api.put<TaskDetail>(`/admin/tasks/${taskId}/checklist/${item.id}`, {
        text: item.text,
        done: item.done,
        ...(item.description !== undefined ? { description: item.description } : {}),
      }),
    // Optimistic tick so the checkbox does not flicker back while saving
    onMutate: async item => {
      await qc.cancelQueries({ queryKey: detailKey })
      const prev = qc.getQueryData<TaskDetail>(detailKey)
      if (prev) {
        qc.setQueryData<TaskDetail>(detailKey, {
          ...prev,
          checklist: prev.checklist.map(i => (i.id === item.id ? { ...i, text: item.text, done: item.done, description: item.description ?? i.description } : i)),
        })
      }
      return { prev }
    },
    onError: (err: ApiError, _item, ctx) => {
      if (ctx?.prev) qc.setQueryData(detailKey, ctx.prev)
      onError(err)
    },
    onSuccess,
  })

  const remove = useMutation({
    mutationFn: (itemId: string) => api.delete<TaskDetail>(`/admin/tasks/${taskId}/checklist/${itemId}`),
    onSuccess,
    onError,
  })

  return { add, update, remove }
}

export function useTaskNotes(taskId: string) {
  return useQuery({
    queryKey: qk.tasks.notes(taskId),
    queryFn: () => api.get<TaskNote[]>(`/admin/tasks/${taskId}/notes`),
  })
}

/** Notes change notesCount on the task detail and on the Kanban card. */
export function useTaskNoteMutations(taskId: string) {
  const qc = useQueryClient()
  const refresh = () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: qk.tasks.notes(taskId) }),
      qc.invalidateQueries({ queryKey: qk.tasks.detail(taskId) }),
      qc.invalidateQueries({ queryKey: qk.tasks.lists }),
      qc.invalidateQueries({ queryKey: qk.dashboard }),
    ])
  const onError = (err: ApiError) => toast.error(errorText(err, 'Không thể lưu ghi chú'))

  const add = useMutation({
    mutationFn: (body: string) => api.post<TaskNote>(`/admin/tasks/${taskId}/notes`, { body }),
    onSuccess: refresh,
    onError,
  })

  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: string }) => api.put<TaskNote>(`/admin/tasks/${taskId}/notes/${id}`, { body }),
    onSuccess: refresh,
    onError,
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/tasks/${taskId}/notes/${id}`),
    onSuccess: () => {
      toast.success('Đã xoá ghi chú')
      return refresh()
    },
    onError,
  })

  return { add, update, remove }
}
