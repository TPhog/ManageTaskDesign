import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { DeliverablesTab } from '@/features/admin/projects/detail/DeliverablesTab'
import { api } from '@/lib/api'
import type { Deliverable, ProjectDetail } from '@/lib/types'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }))
vi.mock('@/lib/api', () => ({ api: { post: vi.fn(), get: vi.fn() } }))

const deliverable: Deliverable = {
  id: 'd1', name: 'Logo chính', quantity: 1, serviceId: null, includedRounds: 2, extraRoundPrice: 0, usedRounds: 0,
  remainingRounds: 2, status: 'IN_PROGRESS', dueDate: null, sortOrder: 1, taskTotal: 0, taskDone: 0, progressPercent: null,
  clientDecision: null, clientDecisionAt: null,
}
const project = (deliverables: Deliverable[]) => ({ id: 'p1', deliverables }) as unknown as ProjectDetail

function renderTab(deliverables: Deliverable[], url = '/x') {
  vi.mocked(api.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 100 })
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[url]}>
        <DeliverablesTab project={project(deliverables)} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('DeliverablesTab', () => {
  beforeEach(() => vi.clearAllMocks())

  it('?new=1 opens the create dialog and the UI never says "deliverable"', async () => {
    renderTab([], '/x?new=1')
    expect(await screen.findByRole('dialog')).toHaveTextContent('Thêm hạng mục')
    expect(document.body.textContent?.toLowerCase()).not.toContain('deliverable')
  })

  it('"Gửi duyệt" asks first instead of calling the API', async () => {
    renderTab([deliverable])
    await userEvent.click(screen.getByRole('button', { name: 'Gửi duyệt Logo chính' }))
    expect(await screen.findByText('Gửi hạng mục cho khách duyệt?')).toBeInTheDocument()
    expect(api.post).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Gửi duyệt' }))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/admin/deliverables/d1/submit-review', { clientMessage: null }))
  })
})
