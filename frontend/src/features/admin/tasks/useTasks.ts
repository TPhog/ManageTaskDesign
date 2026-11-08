import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type {
  PagedResult,
  TaskDetail,
  TaskListItem,
  TaskListParams,
  TaskStatus,
  TaskUpdateInput,
} from '@/lib/types'

function toQuery(params: TaskListParams): string {
  // pageSize is capped at 100 by the API (docs/05 §1.3)
  const search = new URLSearchParams({ pageSize: '100' })
  if (params.projectId) search.set('projectId', params.projectId)
  if (params.status) search.set('status', params.status)
  if (params.due) search.set('due', params.due)
  return search.toString()
}

function errorText(err: ApiError, fallback: string): string {
  const fieldError = err.errors ? Object.values(err.errors).flat()[0] : undefined
  return fieldError ?? err.detail ?? fallback
}

/** Every task write returns the fresh TaskDetail: store it, then refresh the lists that show the task. */
function onTaskSaved(qc: QueryClient, task: TaskDetail | undefined) {
  if (task) qc.setQueryData(qk.tasks.detail(task.id), task)
  return qc.invalidateQueries({ queryKey: qk.tasks.lists })
}

export function useTaskList(params: TaskListParams) {
  return useQuery({
    queryKey: qk.tasks.list(params),
    queryFn: () => api.get<PagedResult<TaskListItem>>(`/admin/tasks?${toQuery(params)}`),
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
    onSettled: () => qc.invalidateQueries({ queryKey: qk.tasks.lists }),
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
    mutationFn: (item: { id: string; text: string; done: boolean }) =>
      api.put<TaskDetail>(`/admin/tasks/${taskId}/checklist/${item.id}`, { text: item.text, done: item.done }),
    // Optimistic tick so the checkbox does not flicker back while saving
    onMutate: async item => {
      await qc.cancelQueries({ queryKey: detailKey })
      const prev = qc.getQueryData<TaskDetail>(detailKey)
      if (prev) {
        qc.setQueryData<TaskDetail>(detailKey, {
          ...prev,
          checklist: prev.checklist.map(i => (i.id === item.id ? { ...i, text: item.text, done: item.done } : i)),
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
