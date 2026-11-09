import { fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { DeliverableDialog } from '@/features/admin/projects/detail/DeliverableDialog'
import type { Deliverable } from '@/lib/types'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn().mockResolvedValue([]), post: vi.fn(), put: vi.fn() } }))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }))

const deliverable: Deliverable = {
  id: 'd1', name: 'Logo chính', quantity: 1, serviceId: null, includedRounds: 2, extraRoundPrice: 0, usedRounds: 0,
  remainingRounds: 2, status: 'NOT_STARTED', dueDate: null, sortOrder: 1, taskTotal: 0, taskDone: 0,
  progressPercent: null, clientDecision: null, clientDecisionAt: null, links: [],
}

function renderDialog(onClose: () => void) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <DeliverableDialog projectId="p1" open onClose={onClose} editing={deliverable} />
    </QueryClientProvider>,
  )
}

describe('DeliverableDialog unsaved links', () => {
  it('closes at once when the links are untouched', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    fireEvent.click(screen.getByRole('button', { name: 'Huỷ' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('asks before dropping link edits that were not saved', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    fireEvent.change(screen.getByLabelText('Địa chỉ liên kết'), { target: { value: 'https://figma.com/file/1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Thêm liên kết' }))
    expect(screen.getByText('Liên kết chưa lưu')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Huỷ' }))
    expect(onClose).not.toHaveBeenCalled()
    const confirm = screen.getByRole('dialog', { name: 'Bỏ liên kết chưa lưu?' })
    fireEvent.click(screen.getByRole('button', { name: 'Bỏ thay đổi' }))
    expect(onClose).toHaveBeenCalledTimes(1)
    expect(confirm).toBeTruthy()
  })
})
