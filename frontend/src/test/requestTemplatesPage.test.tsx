import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import RequestTemplatesPage from '@/features/admin/request-templates/RequestTemplatesPage'
import { api } from '@/lib/api'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn() } }))

const tpl = (id: string, name: string, sortOrder: number) => ({
  id, name, description: '', fields: [], designType: 'POSTER', serviceId: null, serviceName: null, active: true, sortOrder,
  rowVersion: 1, createdAt: '2026-11-13T03:00:00Z', updatedAt: '2026-11-13T03:00:00Z',
})

describe('RequestTemplatesPage reorder (review M-10)', () => {
  it('locks the up/down buttons while a reorder is saving', async () => {
    vi.mocked(api.get).mockResolvedValue([tpl('a', 'Poster', 1), tpl('b', 'Logo', 2), tpl('c', 'Menu', 3)] as never)
    let finish!: () => void
    vi.mocked(api.post).mockReturnValue(new Promise<void>(r => { finish = r }) as never)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={qc}><MemoryRouter><RequestTemplatesPage /></MemoryRouter></QueryClientProvider>)

    fireEvent.click(await screen.findByRole('button', { name: 'Đưa mẫu Logo lên' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Đưa mẫu Menu lên' })).toBeDisabled())
    expect(api.post).toHaveBeenCalledTimes(1)

    finish()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Đưa mẫu Menu lên' })).toBeEnabled())
  })
})
