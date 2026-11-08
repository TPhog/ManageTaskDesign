import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { OriginalCleanupSection } from '@/features/admin/settings/OriginalCleanupSection'
import { api } from '@/lib/api'
import type { OriginalCleanupPreview } from '@/lib/imageTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn() } }))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
import { toast } from 'sonner'

const preview: OriginalCleanupPreview = {
  olderThanMonths: 6, cutoff: '2026-05-12T03:00:00Z', projectCount: 2, imageCount: 30, freedBytes: 40 * 1024 ** 2, digest: 'd1',
  projects: [{ projectId: 'p1', projectName: 'Logo', completedAt: '2026-01-01T00:00:00Z', imageCount: 30, freedBytes: 40 * 1024 ** 2 }],
}

function renderSection() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={qc}><OriginalCleanupSection /></QueryClientProvider>)
}

async function openAndTick() {
  fireEvent.click(await screen.findByRole('button', { name: 'Xoá bản gốc…' }))
  fireEvent.click(screen.getByRole('checkbox', { name: 'Tôi hiểu bản gốc sẽ bị xoá vĩnh viễn' }))
  return screen.getByRole('button', { name: 'Xoá vĩnh viễn bản gốc' })
}

describe('OriginalCleanupSection', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.get).mockResolvedValue(preview)
  })

  it('sends the previewed count, bytes and digest, and submits once on a double click', async () => {
    let resolve!: (v: unknown) => void
    vi.mocked(api.post).mockReturnValue(new Promise(r => { resolve = r }))
    renderSection()
    const confirm = await openAndTick()
    fireEvent.click(confirm)
    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(confirm).toBeDisabled())
    fireEvent.click(confirm)
    expect(api.post).toHaveBeenCalledTimes(1)
    expect(api.post).toHaveBeenCalledWith('/admin/storage/original-cleanup', {
      olderThanMonths: 6, expectedImageCount: 30, expectedFreedBytes: 40 * 1024 ** 2, expectedDigest: 'd1',
    })
    resolve({ projectCount: 2, imageCount: 30, freedBytes: 40 * 1024 ** 2 })
    await waitFor(() => expect(toast.success).toHaveBeenCalled())
  })

  it('on 409 closes the dialog, refetches the preview and asks for a re-check', async () => {
    vi.mocked(api.post).mockRejectedValue({ status: 409, code: 'CONCURRENCY_CONFLICT' })
    renderSection()
    const confirm = await openAndTick()
    const getsBefore = vi.mocked(api.get).mock.calls.length
    fireEvent.click(confirm)
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Danh sách đã thay đổi, xem lại'))
    await waitFor(() => expect(vi.mocked(api.get).mock.calls.length).toBeGreaterThan(getsBefore))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('uses a delete-specific message for other failures', async () => {
    vi.mocked(api.post).mockRejectedValue({ status: 500 })
    renderSection()
    fireEvent.click(await openAndTick())
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Không xoá được bản gốc. Vui lòng thử lại.'))
  })
})
