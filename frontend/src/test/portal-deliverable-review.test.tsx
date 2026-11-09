import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { DeliverableReview } from '@/features/portal/DeliverableReview'
import type { PortalDeliverable } from '@/lib/portalTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(() => new Promise(() => {})), post: vi.fn(), postForm: vi.fn() } }))

const base: PortalDeliverable = {
  id: 'd1', name: 'Logo', quantity: 1, status: 'IN_REVIEW', includedRounds: 2, usedRounds: 0, remainingRounds: 2,
  dueDate: null, progressPercent: null, clientDecision: null, reviewState: 'AWAITING_CLIENT', hasOpenChangeRequest: false,
}
const renderCard = (d: Partial<PortalDeliverable>) => render(
  <QueryClientProvider client={new QueryClient()}><MemoryRouter>
    <DeliverableReview projectId="p1" projectStatus="IN_PROGRESS" deliverable={{ ...base, ...d }} />
  </MemoryRouter></QueryClientProvider>)

describe('DeliverableReview (CF-01/02)', () => {
  it('asks for a decision only while waiting for the client', () => {
    renderCard({})
    expect(screen.getByRole('button', { name: 'Duyệt Logo' })).toHaveClass('h-11')
    expect(screen.getByRole('button', { name: 'Yêu cầu sửa Logo' })).toBeInTheDocument()
  })

  it('says the approval is waiting for the designer and hides the buttons', () => {
    renderCard({ clientDecision: 'APPROVED', reviewState: 'CLIENT_APPROVED' })
    expect(screen.getByText(/Bạn đã duyệt — đang chờ nhà thiết kế xác nhận/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Duyệt Logo' })).not.toBeInTheDocument()
  })

  it('says the designer is working on the change request and hides the buttons', () => {
    renderCard({ reviewState: 'CHANGES_IN_PROGRESS', hasOpenChangeRequest: true })
    expect(screen.getByText(/Nhà thiết kế đang sửa theo yêu cầu của bạn/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Duyệt Logo' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yêu cầu sửa Logo' })).not.toBeInTheDocument()
  })
})
