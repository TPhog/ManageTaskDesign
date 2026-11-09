import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import PortalHomePage from '@/features/portal/PortalHomePage'
import { api } from '@/lib/api'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

const designer = { displayName: 'Minh Anh', logoUrl: null, contactPhone: null, contactEmail: null, responseTime: 'trong 24 giờ' }
const project = {
  id: 'p1', name: 'Tiệm bánh Mây', status: 'IN_PROGRESS', designType: 'LOGO', dueDate: null, hasOpenQuotation: false, openQuotationId: null,
  deliverableSummary: { total: 1, delivered: 0, clientApproved: 0, awaitingClient: 0 },
}

function renderHome(intakeUrl: string | null, projects: unknown[]) {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    if (url === '/portal/me') return { clientName: 'Hoa', designer: { ...designer, intakeUrl } }
    if (url === '/portal/projects') return projects
    return []
  })
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><MemoryRouter><PortalHomePage /></MemoryRouter></QueryClientProvider>)
}

describe('PortalHomePage "Đặt thiết kế mới" (plan 13)', () => {
  it('links to the public form under the project list', async () => {
    renderHome('http://localhost:5173/f/abc123', [project])
    expect(await screen.findByText('Tiệm bánh Mây')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Đặt thiết kế mới' })).toHaveAttribute('href', 'http://localhost:5173/f/abc123')
  })

  it('offers it in the empty state too', async () => {
    renderHome('/f/abc123', [])
    expect(await screen.findByText('Chưa có dự án nào')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Đặt thiết kế mới' })).toHaveAttribute('href', '/f/abc123')
  })

  it('is hidden while the designer has the form turned off', async () => {
    renderHome(null, [project])
    expect(await screen.findByText('Tiệm bánh Mây')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Đặt thiết kế mới' })).not.toBeInTheDocument()
  })
})
