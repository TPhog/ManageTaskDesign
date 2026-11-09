import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { PortalTodoList } from '@/features/portal/PortalTodoList'
import { PortalWelcome } from '@/features/portal/PortalWelcome'

describe('portal home (CF-14)', () => {
  it('greets the client with the studio initials', () => {
    render(<PortalWelcome clientName="Tiệm bánh Mây" studioName="Minh Anh Design Studio" />)
    expect(screen.getByText('MA')).toBeInTheDocument()
    expect(screen.getByText(/Chào Tiệm bánh Mây!/)).toBeInTheDocument()
    expect(screen.getByText(/Minh Anh Design Studio gửi bạn trang theo dõi dự án/)).toBeInTheDocument()
  })

  it('lists what the client has to do as large links, or says there is nothing', () => {
    const { rerender } = render(<MemoryRouter><PortalTodoList items={[
      { key: 'a', kind: 'QUOTATION', label: 'Xem và đồng ý báo giá BG-2026-0001', to: '/portal/quotations/q1' },
    ]} /></MemoryRouter>)
    const region = screen.getByRole('region', { name: 'Việc bạn cần làm' })
    expect(screen.getByRole('link', { name: /Xem và đồng ý báo giá BG-2026-0001/ })).toHaveClass('min-h-11')
    expect(region).toBeInTheDocument()

    rerender(<MemoryRouter><PortalTodoList items={[]} /></MemoryRouter>)
    expect(screen.getByText('Hiện chưa có việc gì cần bạn làm. Nhà thiết kế sẽ báo khi có bản mới.')).toBeInTheDocument()
  })
})
