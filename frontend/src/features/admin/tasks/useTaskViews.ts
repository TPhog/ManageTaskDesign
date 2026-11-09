import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { SavedTaskView, TaskViewConfig } from '@/lib/types'

function viewError(err: ApiError, fallback: string): string {
  // 409 CONFLICT: duplicate name (case-insensitive) or the 20-view cap
  if (err.status === 409) return 'Đã có chế độ xem trùng tên hoặc đã đủ 20 chế độ xem'
  return (err.errors ? Object.values(err.errors).flat()[0] : undefined) ?? err.detail ?? fallback
}

export function useTaskViews() {
  return useQuery({
    queryKey: qk.taskViews,
    queryFn: () => api.get<{ views: SavedTaskView[]; last: TaskViewConfig | null }>('/admin/task-views'),
  })
}

export function useSaveTaskView() {
  const qc = useQueryClient()
  return useMutation<SavedTaskView, ApiError, { name: string; config: TaskViewConfig }>({
    mutationFn: body => api.post<SavedTaskView>('/admin/task-views', body),
    onSuccess: () => {
      toast.success('Đã lưu chế độ xem')
      return qc.invalidateQueries({ queryKey: qk.taskViews })
    },
    onError: err => toast.error(viewError(err, 'Không thể lưu chế độ xem')),
  })
}

export function useUpdateTaskView() {
  const qc = useQueryClient()
  return useMutation<SavedTaskView, ApiError, { id: string; name: string; config: TaskViewConfig }>({
    mutationFn: ({ id, ...body }) => api.put<SavedTaskView>(`/admin/task-views/${id}`, body),
    onSuccess: () => {
      toast.success('Đã lưu chế độ xem')
      return qc.invalidateQueries({ queryKey: qk.taskViews })
    },
    onError: err => toast.error(viewError(err, 'Không thể lưu chế độ xem')),
  })
}

export function useDeleteTaskView() {
  const qc = useQueryClient()
  return useMutation<void, ApiError, string>({
    mutationFn: id => api.delete<void>(`/admin/task-views/${id}`),
    onSuccess: () => {
      toast.success('Đã xoá chế độ xem')
      return qc.invalidateQueries({ queryKey: qk.taskViews })
    },
    onError: err => toast.error(viewError(err, 'Không thể xoá chế độ xem')),
  })
}

/** Remembers the view in use; silent (it runs in the background on every filter change). */
export function useSaveLastView() {
  const qc = useQueryClient()
  return useMutation<void, ApiError, TaskViewConfig>({
    mutationFn: config => api.put<void>('/admin/task-views/last', { config }),
    // keep the cached `last` current, so coming back to the page restores this view and not an older one
    onSuccess: (_res, config) =>
      qc.setQueryData<{ views: SavedTaskView[]; last: TaskViewConfig | null }>(qk.taskViews, old => (old ? { ...old, last: config } : old)),
  })
}
