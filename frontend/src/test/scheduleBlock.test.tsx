import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ScheduleView } from '@/features/admin/dashboard/ScheduleBlock'
import type { DashboardSchedule, ScheduleTask } from '@/lib/dashboardTypes'

const task = (id: string, dueDate: string): ScheduleTask => ({
  id, title: `Task ${id}`, status: 'TODO', priority: 'MEDIUM', dueDate, sortOrder: 1024,
  projectId: 'p1', projectName: 'Logo ABC', clientName: 'Khách A',
})
const day = (date: string, tasks: ScheduleTask[] = []) => ({ date, tasks, hiddenTasks: 0, deadlines: [] })
const schedule = (over: Partial<DashboardSchedule> = {}): DashboardSchedule => ({
  today: '2030-05-15', undatedOpenTasks: 0,
  overdue: { total: 2, items: [task('a', '2030-05-10'), task('b', '2030-05-14')] },
  days: [day('2030-05-15', [task('c', '2030-05-15')]), day('2030-05-16'), day('2030-05-17'), day('2030-05-18'), day('2030-05-19'), day('2030-05-20'), day('2030-05-21')],
  ...over,
})

describe('ScheduleView', () => {
  it('shows the overdue bucket with a text label, today first, and ticks a task through onComplete', async () => {
    const onComplete = vi.fn()
    render(<MemoryRouter><ScheduleView schedule={schedule()} onComplete={onComplete} onOpenTask={vi.fn()} /></MemoryRouter>)
    const overdue = screen.getByRole('region', { name: 'Quá hạn' })
    expect(within(overdue).getByText(/Quá hạn \(2\)/)).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 3 })[1]).toHaveTextContent('Hôm nay · 15/05')
    await userEvent.click(within(overdue).getByRole('checkbox', { name: 'Đánh dấu xong: Task a' }))
    expect(onComplete).toHaveBeenCalledWith(expect.objectContaining({ id: 'a' }))
  })

  it('opens the task drawer from the title and collapses empty days into one line', async () => {
    const onOpenTask = vi.fn()
    render(<MemoryRouter><ScheduleView schedule={schedule()} onComplete={vi.fn()} onOpenTask={onOpenTask} /></MemoryRouter>)
    await userEvent.click(screen.getByRole('button', { name: 'Task c' }))
    expect(onOpenTask).toHaveBeenCalledWith('c')
    expect(screen.getByText(/Không có việc:/)).toHaveTextContent('16/05')
  })

  it('shows the undated-tasks hint as a link', () => {
    render(<MemoryRouter><ScheduleView schedule={schedule({ undatedOpenTasks: 7 })} onComplete={vi.fn()} onOpenTask={vi.fn()} /></MemoryRouter>)
    expect(screen.getByRole('link', { name: '7 công việc chưa đặt hạn' })).toHaveAttribute('href', '/admin/tasks?due=all')
  })

  it('links to the full overdue list when more are overdue than shown', () => {
    const s = schedule({ overdue: { total: 15, items: [task('a', '2030-05-10')] } })
    render(<MemoryRouter><ScheduleView schedule={s} onComplete={vi.fn()} onOpenTask={vi.fn()} /></MemoryRouter>)
    expect(screen.getByRole('link', { name: 'Xem tất cả' })).toHaveAttribute('href', '/admin/tasks?due=overdue')
  })

  it('explains an empty week', () => {
    const s = schedule({ overdue: { total: 0, items: [] }, days: schedule().days.map(d => ({ ...d, tasks: [] })) })
    render(<MemoryRouter><ScheduleView schedule={s} onComplete={vi.fn()} onOpenTask={vi.fn()} /></MemoryRouter>)
    expect(screen.getByText(/Tuần này chưa có việc nào/)).toBeInTheDocument()
  })
})
