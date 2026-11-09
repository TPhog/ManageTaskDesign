import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { QuotationDecision } from '@/features/portal/QuotationDecision'
import { api } from '@/lib/api'
import type { PortalQuotationDetail } from '@/lib/portalTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

const q = { id: 'q1', status: 'SENT', effectiveStatus: 'SENT', paymentStatus: 'UNPAID', clientAcceptedAt: null,
  clientPaymentNotice: null, snapshot: { number: 'BG-2026-0001', total: 7_020_000 } } as unknown as PortalQuotationDetail
const renderIt = (over: Partial<PortalQuotationDetail> = {}) =>
  render(<QueryClientProvider client={new QueryClient()}><QuotationDecision quotation={{ ...q, ...over }} /></QueryClientProvider>)

describe('QuotationDecision (CF-03)', () => {
  it('records the agreement after a confirmation', async () => {
    vi.mocked(api.post).mockResolvedValue({ ...q, clientAcceptedAt: '2026-11-08T03:00:00Z' })
    renderIt()

    fireEvent.click(screen.getByRole('button', { name: 'Đồng ý báo giá' }))
    fireEvent.click(screen.getByRole('button', { name: 'Đồng ý' }))

    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/portal/quotations/q1/accept', {}))
  })

  it('shows the waiting state instead of the button once agreed', () => {
    renderIt({ clientAcceptedAt: '2026-11-08T03:00:00Z' })
    expect(screen.queryByRole('button', { name: 'Đồng ý báo giá' })).not.toBeInTheDocument()
    expect(screen.getByText(/Bạn đã đồng ý lúc .* — đang chờ nhà thiết kế xác nhận/)).toBeInTheDocument()
  })

  it('renders nothing for an expired or confirmed quotation', () => {
    const { container, rerender } = renderIt({ effectiveStatus: 'EXPIRED' })
    expect(container).toBeEmptyDOMElement()
    rerender(<QueryClientProvider client={new QueryClient()}><QuotationDecision quotation={{ ...q, status: 'ACCEPTED', effectiveStatus: 'ACCEPTED' }} /></QueryClientProvider>)
    expect(container).toBeEmptyDOMElement()
  })
})
