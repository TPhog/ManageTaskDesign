import type { ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { toast } from 'sonner'
import { useCompleteScheduleTask } from '@/features/admin/dashboard/useDashboardBlocks'
import { api } from '@/lib/api'
import type { DashboardSchedule, ScheduleTask } from '@/lib/dashboardTypes'
import { qk } from '@/lib/queryKeys'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
vi.mock('@/lib/api', () => ({ api: { post: vi.fn(), get: vi.fn() } }))

const task = (id: string, dueDate = '2030-05-10'): ScheduleTask => ({
  id, title: `Task ${id}`, status: 'IN_PROGRESS', priority: 'MEDIUM', dueDate, sortOrder: 2048,
  projectId: 'p1', projectName: 'Logo ABC', clientName: 'Khách A',
})
const day = (date: string) => ({ date, tasks: [], hiddenTasks: 0, deadlines: [] })
const schedule = (): DashboardSchedule => ({
  today: '2030-05-15', undatedOpenTasks: 0,
  overdue: { total: 2, items: [task('a'), task('b')] },
  days: [day('2030-05-15'), day('2030-05-16')],
})

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(qk.dashboardBlocks.schedule, schedule())
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  const hook = renderHook(() => useCompleteScheduleTask(), { wrapper })
  const ids = () => qc.getQueryData<DashboardSchedule>(qk.dashboardBlocks.schedule)!.overdue.items.map(t => t.id)
  return { qc, hook, ids, wrapper }
}

type SuccessCall = [string, { action: { label: string; onClick: () => void } }]

describe('useCompleteScheduleTask', () => {
  beforeEach(() => vi.clearAllMocks())

  it('removes the row optimistically, moves the task to DONE, offers undo and invalidates the dashboard', async () => {
    let resolve!: (v: unknown) => void
    vi.mocked(api.post).mockReturnValueOnce(new Promise(r => { resolve = r }))
    const { qc, hook, ids } = setup()
    const invalidate = vi.spyOn(qc, 'invalidateQueries')

    act(() => { hook.result.current.mutate({ task: task('a') }) })
    await waitFor(() => expect(ids()).toEqual(['b']))
    expect(api.post).toHaveBeenCalledWith('/admin/tasks/a/move', { status: 'DONE', sortOrder: 2048 })

    resolve({})
    await waitFor(() => expect(toast.success).toHaveBeenCalled())
    const [message, opts] = vi.mocked(toast.success).mock.calls[0] as unknown as SuccessCall
    expect(message).toBe('Đã hoàn thành "Task a"')
    expect(opts.action.label).toBe('Hoàn tác')
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: qk.dashboard }))
  })

  it('undo moves the task back to its previous status', async () => {
    vi.mocked(api.post).mockResolvedValue({})
    const { hook } = setup()
    await act(async () => { await hook.result.current.mutateAsync({ task: task('a') }) })
    const [, opts] = vi.mocked(toast.success).mock.calls[0] as unknown as SuccessCall
    act(() => opts.action.onClick())
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/admin/tasks/a/move', { status: 'IN_PROGRESS', sortOrder: 2048 }))
  })

  it('on error restores only the failed task and keeps another in-flight tick removed', async () => {
    let fail!: (e: unknown) => void
    vi.mocked(api.post)
      .mockReturnValueOnce(new Promise((_, rej) => { fail = rej }))
      .mockReturnValueOnce(new Promise(() => {}))
    const { hook, ids, wrapper } = setup()
    const second = renderHook(() => useCompleteScheduleTask(), { wrapper })

    act(() => { hook.result.current.mutate({ task: task('a') }) })
    act(() => { second.result.current.mutate({ task: task('b') }) })
    await waitFor(() => expect(ids()).toEqual([]))

    fail({ status: 500 })
    await waitFor(() => expect(ids()).toEqual(['a']))
    expect(toast.error).toHaveBeenCalledWith('Không thể đánh dấu xong công việc')
  })
})
