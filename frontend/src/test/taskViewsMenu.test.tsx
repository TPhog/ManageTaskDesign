import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TaskViewsMenu } from '@/features/admin/tasks/TaskViewsMenu'
import { DEFAULT_VIEW_CONFIG } from '@/features/admin/tasks/taskUtils'
import { api } from '@/lib/api'
import type { TaskViewConfig } from '@/lib/types'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
import { toast } from 'sonner'

const urgent: TaskViewConfig = { ...DEFAULT_VIEW_CONFIG, groupBy: 'due' }

function setup(current: TaskViewConfig, ready = true) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const ui = (c: TaskViewConfig, r: boolean) => (
    <QueryClientProvider client={qc}><TaskViewsMenu current={c} onApply={() => {}} ready={r} /></QueryClientProvider>
  )
  const view = render(ui(current, ready))
  return { rerender: (c: TaskViewConfig, r = true) => view.rerender(ui(c, r)) }
}

describe('TaskViewsMenu', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.get).mockResolvedValue({ views: [{ id: 'v1', name: 'Việc gấp', config: urgent, sortOrder: 0 }], last: urgent })
  })

  it('offers "Cập nhật" for a view restored after a reload once its filters change', async () => {
    const { rerender } = setup(urgent)
    expect(await screen.findByRole('button', { name: 'Chế độ xem: Việc gấp' })).toBeInTheDocument()
    rerender({ ...urgent, groupBy: 'priority' })
    fireEvent.click(screen.getByRole('button', { name: 'Chế độ xem' }))
    expect(await screen.findByRole('button', { name: 'Cập nhật “Việc gấp”' })).toBeInTheDocument()
  })

  it('does not adopt a view that only matched the defaults shown before the restore', async () => {
    vi.mocked(api.get).mockResolvedValue({ views: [{ id: 'v0', name: 'Mặc định', config: DEFAULT_VIEW_CONFIG, sortOrder: 0 }], last: urgent })
    const { rerender } = setup(DEFAULT_VIEW_CONFIG, false)
    await waitFor(() => expect(api.get).toHaveBeenCalled())
    await screen.findByRole('button', { name: 'Chế độ xem: Mặc định' })
    rerender(urgent, true)
    fireEvent.click(screen.getByRole('button', { name: 'Chế độ xem' }))
    await screen.findByRole('button', { name: 'Lưu chế độ xem hiện tại…' })
    expect(screen.queryByRole('button', { name: /Cập nhật/ })).toBeNull()
  })

  it('shows the server detail of a 409', async () => {
    vi.mocked(api.post).mockRejectedValue({ status: 409, code: 'CONFLICT', detail: 'Đã có chế độ xem trùng tên' })
    setup({ ...urgent, groupBy: 'status' })
    fireEvent.click(await screen.findByRole('button', { name: 'Chế độ xem' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Lưu chế độ xem hiện tại…' }))
    fireEvent.change(screen.getByLabelText('Tên chế độ xem'), { target: { value: 'Việc gấp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Đã có chế độ xem trùng tên'))
  })
})
