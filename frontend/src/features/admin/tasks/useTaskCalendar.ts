import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { CalendarData, TaskDetail } from '@/lib/types'
import { addDays } from './taskUtils'

/** Whole days from `a` to `b` ('YYYY-MM-DD'). */
const dayDiff = (a: string, b: string) => Math.round((Date.parse(`${b}T00:00:00Z`) - Date.parse(`${a}T00:00:00Z`)) / 86_400_000)

export function useTaskCalendar(from: string, to: string, projectId?: string) {
  const search = new URLSearchParams({ from, to })
  if (projectId) search.set('projectId', projectId)
  return useQuery({
    queryKey: qk.tasks.calendar(from, to, projectId),
    queryFn: () => api.get<CalendarData>(`/admin/tasks/calendar?${search.toString()}`),
    // keep the previous month on screen while the next one loads (no flash of an empty grid)
    placeholderData: keepPreviousData,
  })
}

/** Calendar drag: PUT due-date. Optimistic on every calendar key — the start date moves by the same number of days. */
export function useRescheduleTask() {
  const qc = useQueryClient()
  return useMutation<TaskDetail, ApiError, { id: string; dueDate: string }, { snapshots: [readonly unknown[], CalendarData | undefined][] }>({
    mutationFn: ({ id, dueDate }) => api.put<TaskDetail>(`/admin/tasks/${id}/due-date`, { dueDate }),
    onMutate: async ({ id, dueDate }) => {
      await qc.cancelQueries({ queryKey: qk.tasks.calendarAll })
      const snapshots = qc.getQueriesData<CalendarData>({ queryKey: qk.tasks.calendarAll })
      for (const [key, data] of snapshots) {
        if (!data) continue
        qc.setQueryData<CalendarData>(key, {
          ...data,
          tasks: data.tasks.map(t => {
            if (t.id !== id) return t
            // mirrors the server: a start→due span keeps its length; a start-only task never ends before it starts
            const startDate = t.startDate && t.dueDate
              ? addDays(t.startDate, dayDiff(t.dueDate, dueDate))
              : t.startDate && dueDate < t.startDate ? dueDate : t.startDate
            return { ...t, dueDate, startDate }
          }),
        })
      }
      return { snapshots }
    },
    onError: (err, _vars, ctx) => {
      for (const [key, data] of ctx?.snapshots ?? []) qc.setQueryData(key, data)
      toast.error(err.detail ?? 'Không thể đổi hạn')
    },
    onSuccess: task => {
      qc.setQueryData(qk.tasks.detail(task.id), task)
      toast.success('Đã đổi hạn')
    },
    onSettled: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: qk.tasks.lists }),
        qc.invalidateQueries({ queryKey: qk.tasks.calendarAll }),
        qc.invalidateQueries({ queryKey: qk.projects.all }),
        qc.invalidateQueries({ queryKey: qk.dashboard }),
      ]),
  })
}
