import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, useLocation } from 'react-router-dom'
import TasksPage from '@/features/admin/tasks/TasksPage'
import { DEFAULT_VIEW_CONFIG } from '@/features/admin/tasks/taskUtils'
import { api } from '@/lib/api'
import type { TaskViewConfig } from '@/lib/types'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('sonner', () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }))

const last: TaskViewConfig = { ...DEFAULT_VIEW_CONFIG, groupBy: 'status', due: 'week' }

function Location() {
  const l = useLocation()
  return <output data-testid="location">{l.search}</output>
}

function renderAt(url: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[url]}>
        <TasksPage />
        <Location />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const savedLast = () => vi.mocked(api.put).mock.calls.filter(([url]) => url === '/admin/task-views/last').map(([, body]) => (body as { config: TaskViewConfig }).config)

describe('TasksPage last-view gate', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.get).mockImplementation(async (url: string) =>
      url === '/admin/task-views'
        ? { views: [], last }
        : { items: [], total: 0, page: 1, pageSize: 50, groups: [], truncated: false })
    vi.mocked(api.put).mockResolvedValue(undefined)
  })

  it('restores the last view when the URL has no view keys, and never saves the defaults over it', async () => {
    renderAt('/admin/tasks')
    await waitFor(() => expect(screen.getByTestId('location').textContent).toContain('groupBy=status'))
    expect(screen.getByTestId('location').textContent).toContain('due=week')
    await waitFor(() => expect(savedLast().length).toBeGreaterThan(0), { timeout: 3000 })
    expect(savedLast().every(c => c.groupBy === 'status' && c.due === 'week')).toBe(true)
  })

  it('keeps the URL filters when the page opens with view keys', async () => {
    renderAt('/admin/tasks?due=overdue')
    await waitFor(() => expect(savedLast().length).toBeGreaterThan(0), { timeout: 3000 })
    expect(screen.getByTestId('location').textContent).toBe('?due=overdue')
    expect(savedLast().at(-1)).toMatchObject({ due: 'overdue', groupBy: 'none' })
  })
})
