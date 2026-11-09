import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TaskBlockedField } from '@/features/admin/tasks/TaskBlockedField'
import { api } from '@/lib/api'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn() } }))

function renderField(saved: { byTaskId: string | null; byTaskTitle: string | null; byTaskDone: boolean } | null) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <TaskBlockedField
        projectId="p1"
        taskId="t1"
        value={{ reason: '', byTaskId: saved?.byTaskId ?? null }}
        onChange={() => {}}
        saved={saved}
      />
    </QueryClientProvider>,
  )
}

describe('TaskBlockedField', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // the first page of the project does not hold the saved blocker
    vi.mocked(api.get).mockResolvedValue({ items: [], total: 150, page: 1, pageSize: 20, groups: [], truncated: false })
  })

  it('shows the saved blocker even when it is not among the loaded tasks, with the done hint', async () => {
    renderField({ byTaskId: 'b9', byTaskTitle: 'Chốt màu chủ đạo', byTaskDone: true })
    expect(screen.getByRole('combobox', { name: 'Bị chặn bởi công việc' })).toHaveTextContent('Chốt màu chủ đạo (Xong)')
    expect(screen.getByRole('status')).toHaveTextContent('Việc chặn đã xong')
    await waitFor(() => expect(api.get).toHaveBeenCalled())
  })

  it('searches the project on the server instead of loading a capped page', async () => {
    renderField(null)
    await waitFor(() => expect(api.get).toHaveBeenCalled())
    const url = vi.mocked(api.get).mock.calls[0][0] as string
    expect(url).toContain('projectId=p1')
    expect(url).toContain('pageSize=20')
    expect(screen.getByRole('combobox', { name: 'Bị chặn bởi công việc' })).toHaveTextContent('Không chọn')
  })
})
