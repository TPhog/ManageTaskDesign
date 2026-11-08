import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ClientActivityView } from '@/features/admin/dashboard/ClientActivityBlock'

const renderView = (items: Parameters<typeof ClientActivityView>[0]['activity']['items']) =>
  render(<MemoryRouter><ClientActivityView activity={{ items }} /></MemoryRouter>)

describe('ClientActivityView', () => {
  it('links each row to the server-built href and shows who and where', () => {
    renderView([
      { id: 1, occurredAt: '2030-05-15T03:20:00+00:00', kind: 'REQUEST_CREATED', text: 'Khách gửi yêu cầu mới', clientName: 'Chị Lan', projectName: 'Logo ABC', href: '/admin/requests/r1' },
      { id: 2, occurredAt: '2030-05-15T04:00:00+00:00', kind: 'QUOTATION_ACCEPTED', text: 'Khách chấp nhận báo giá', clientName: null, projectName: null, href: '/admin/quotations/q1' },
    ])
    expect(screen.getByRole('link', { name: /Khách gửi yêu cầu mới/ })).toHaveAttribute('href', '/admin/requests/r1')
    expect(screen.getByText(/Chị Lan · Logo ABC/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Khách chấp nhận báo giá/ })).toHaveAttribute('href', '/admin/quotations/q1')
  })

  it('shows the empty state', () => {
    renderView([])
    expect(screen.getByText('Chưa có hoạt động nào từ khách.')).toBeInTheDocument()
  })
})
