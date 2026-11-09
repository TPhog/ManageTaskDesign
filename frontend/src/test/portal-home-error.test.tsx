import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import PortalHomePage from '@/features/portal/PortalHomePage'
import { api } from '@/lib/api'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

const me = { clientName: 'Hoa', designer: { displayName: 'Minh Anh', logoUrl: null, contactPhone: null, contactEmail: null, responseTime: 'trong 24 giờ' } }

describe('PortalHomePage load failure (review I-3)', () => {
  it('shows an error with retry instead of "nothing to do" / "no projects"', async () => {
    let fail = true
    vi.mocked(api.get).mockImplementation(async (url: string) => {
      if (url === '/portal/me') return me
      if (fail) throw { status: 0 }
      return []
    })
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={qc}><MemoryRouter><PortalHomePage /></MemoryRouter></QueryClientProvider>)

    expect(await screen.findByText(/Không tải được việc cần làm/)).toBeInTheDocument()
    expect(screen.getByText(/Không tải được danh sách dự án/)).toBeInTheDocument()
    expect(screen.queryByText(/Hiện chưa có việc gì cần bạn làm/)).not.toBeInTheDocument()
    expect(screen.queryByText('Chưa có dự án nào')).not.toBeInTheDocument()

    fail = false
    fireEvent.click(screen.getAllByRole('button', { name: 'Thử lại' })[0])
    await waitFor(() => expect(screen.getByText(/Hiện chưa có việc gì cần bạn làm/)).toBeInTheDocument())
    expect(screen.getByText('Chưa có dự án nào')).toBeInTheDocument()
  })
})
