import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import type {
  ClientActivity, DashboardSchedule, MoneyInsights, MoneyPeriod, ProjectHealth, ScheduleTask, SetupChecklist,
} from '@/lib/dashboardTypes'
import { qk } from '@/lib/queryKeys'
import type { TaskDetail } from '@/lib/types'
import { useMoveTask } from '@/features/admin/tasks/useTasks'
import { removeTaskFromSchedule } from './dashboardUtils'

/** All dashboard blocks use staleTime 0 like useDashboard: they summarise other pages, refetch on each visit. */
export function useDashboardSchedule() {
  return useQuery({
    queryKey: qk.dashboardBlocks.schedule,
    queryFn: () => api.get<DashboardSchedule>('/admin/dashboard/schedule'),
    staleTime: 0,
  })
}

/** Tick a task DONE through the existing move endpoint; optimistic removal, undo toast. */
export function useCompleteScheduleTask() {
  const qc = useQueryClient()
  const undo = useMoveTask()
  return useMutation({
    mutationFn: ({ task }: { task: ScheduleTask }) =>
      api.post<TaskDetail>(`/admin/tasks/${task.id}/move`, { status: 'DONE', sortOrder: task.sortOrder }),
    onMutate: async ({ task }) => {
      await qc.cancelQueries({ queryKey: qk.dashboardBlocks.schedule })
      const snapshot = qc.getQueryData<DashboardSchedule>(qk.dashboardBlocks.schedule)
      if (snapshot) qc.setQueryData(qk.dashboardBlocks.schedule, removeTaskFromSchedule(snapshot, task.id))
      return { snapshot }
    },
    onError: (_err: ApiError, _vars, ctx) => {
      if (ctx?.snapshot) qc.setQueryData(qk.dashboardBlocks.schedule, ctx.snapshot)
      toast.error('Không thể đánh dấu xong công việc')
    },
    onSuccess: (_data, { task }) => {
      toast.success(`Đã hoàn thành "${task.title}"`, {
        action: {
          label: 'Hoàn tác',
          onClick: () => undo.mutate({ id: task.id, status: task.status, sortOrder: task.sortOrder }),
        },
      })
    },
    onSettled: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: qk.dashboard }),
        qc.invalidateQueries({ queryKey: qk.tasks.lists }),
        qc.invalidateQueries({ queryKey: qk.projects.all }),
      ]),
  })
}

export function useSetupChecklist() {
  return useQuery({
    queryKey: qk.dashboardBlocks.setup,
    queryFn: () => api.get<SetupChecklist>('/admin/dashboard/setup'),
    staleTime: 0,
  })
}

export function useDismissSetup() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<void>('/admin/dashboard/setup/dismiss', undefined),
    onSuccess: () => {
      qc.setQueryData<SetupChecklist>(qk.dashboardBlocks.setup, prev => prev && { ...prev, dismissed: true })
    },
  })
}

export function useProjectHealth() {
  return useQuery({
    queryKey: qk.dashboardBlocks.health,
    queryFn: () => api.get<ProjectHealth>('/admin/dashboard/project-health'),
    staleTime: 0,
  })
}

export function useMoneyInsights(period: MoneyPeriod) {
  return useQuery({
    queryKey: qk.dashboardBlocks.money(period),
    queryFn: () => api.get<MoneyInsights>(`/admin/dashboard/money?period=${period}`),
    staleTime: 0,
    placeholderData: keepPreviousData,
  })
}

export function useClientActivity(limit = 10) {
  return useQuery({
    queryKey: [...qk.dashboardBlocks.activity, limit] as const,
    queryFn: () => api.get<ClientActivity>(`/admin/dashboard/client-activity?limit=${limit}`),
    staleTime: 0,
  })
}
