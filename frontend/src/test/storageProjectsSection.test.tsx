import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { StorageProjectsSection } from '@/features/admin/settings/StorageProjectsSection'
import { api } from '@/lib/api'
import type { StorageIntake, StorageProject } from '@/lib/imageTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn() } }))

function renderSection(projects: StorageProject[], intake: StorageIntake) {
  vi.mocked(api.get).mockImplementation(async (path: string) => {
    if (path === '/admin/storage/projects') return projects
    if (path === '/admin/storage/intake') return intake
    throw new Error(`unexpected GET ${path}`)
  })
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><MemoryRouter><StorageProjectsSection onSelect={() => {}} /></MemoryRouter></QueryClientProvider>)
}

describe('StorageProjectsSection — unconverted intake photos (R2 M7)', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows them as a "Khách mới (chưa chuyển)" group linking to the inbox, even with no project images', async () => {
    renderSection([], { submissionCount: 2, imageCount: 3, totalBytes: 3 * 1024 ** 2 })
    expect(await screen.findByText('Khách mới (chưa chuyển)')).toBeInTheDocument()
    expect(screen.getByText(/2 hồ sơ · 3 ảnh/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mở Khách mới' })).toHaveAttribute('href', '/admin/intake')
    expect(screen.queryByText('Chưa có ảnh nào')).not.toBeInTheDocument()
  })

  it('hides the group when there is nothing to free', async () => {
    renderSection([], { submissionCount: 0, imageCount: 0, totalBytes: 0 })
    expect(await screen.findByText('Chưa có ảnh nào')).toBeInTheDocument()
    expect(screen.queryByText('Khách mới (chưa chuyển)')).not.toBeInTheDocument()
  })
})
