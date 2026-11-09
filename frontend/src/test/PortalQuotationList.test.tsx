import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { PortalQuotationList } from '@/features/portal/PortalQuotationList'
import type { PortalQuotationListItem } from '@/lib/portalTypes'

function item(overrides: Partial<PortalQuotationListItem> = {}): PortalQuotationListItem {
  return {
    id: 'q1', projectId: 'p1', projectName: 'Nhận diện ABC', number: 'BG-2026-0007', version: 2, kind: 'INITIAL',
    title: 'Báo giá logo', status: 'SENT', effectiveStatus: 'EXPIRED', paymentStatus: 'UNPAID',
    total: 2_400_000, depositAmount: 1_200_000, remainingAmount: 1_200_000, issueDate: '2026-11-01', expiryDate: '2026-11-08',
    clientAcceptedAt: null, clientPaymentNotice: null, ...overrides,
  }
}

describe('PortalQuotationList', () => {
  it('links each quotation and shows its effective status', () => {
    render(<MemoryRouter><PortalQuotationList items={[item()]} showProject /></MemoryRouter>)

    const link = screen.getByRole('link', { name: /Báo giá logo/ })
    expect(link).toHaveAttribute('href', '/portal/quotations/q1')
    expect(link).toHaveTextContent('BG-2026-0007 · Phiên bản 2 · Nhận diện ABC')
    expect(link).toHaveTextContent('Đã hết hạn')
    expect(link).not.toHaveTextContent('Chưa thanh toán')
  })

  it('shows the payment status once the quotation is accepted', () => {
    render(
      <MemoryRouter>
        <PortalQuotationList items={[item({ status: 'ACCEPTED', effectiveStatus: 'ACCEPTED', paymentStatus: 'DEPOSIT_PAID' })]} />
      </MemoryRouter>,
    )

    const link = screen.getByRole('link')
    expect(link).toHaveTextContent('Đã nhận cọc')
    expect(link).not.toHaveTextContent('Nhận diện ABC')
  })
})
