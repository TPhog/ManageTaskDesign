import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'
import { PortalRequestForm, type SentRequestSummary } from '@/features/portal/PortalRequestForm'
import { RequestSentSummary } from '@/features/portal/RequestSentSummary'
import { api } from '@/lib/api'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

function wrap(ui: ReactNode, me = { clientName: 'Hoa', designer: { displayName: 'Minh Anh', logoUrl: null, contactPhone: null, contactEmail: null, responseTime: 'trong 24 giờ' } }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } })
  qc.setQueryData(['portal', 'me'], me)
  return render(<QueryClientProvider client={qc}><MemoryRouter>{ui}</MemoryRouter></QueryClientProvider>)
}

const logo = { id: 'd1', name: 'Logo', includedRounds: 2, remainingRounds: 2 }

describe('PortalRequestForm (CF-11/12)', () => {
  beforeEach(() => vi.mocked(api.postForm).mockReset())

  it('in a product card: no project or product select, says it uses one free revision', () => {
    wrap(<PortalRequestForm projects={[{ id: 'p1', name: 'Tiệm bánh Mây' }]} fixedDeliverable={logo} onSent={vi.fn()} />)

    expect(screen.queryByLabelText('Dự án')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Sản phẩm cần sửa (tuỳ chọn)')).not.toBeInTheDocument()
    expect(screen.getByText('Yêu cầu này dùng 1 lượt chỉnh sửa miễn phí (còn 2). Lỗi do nhà thiết kế thì không tính.')).toBeInTheDocument()
    expect(screen.getByLabelText('Ảnh minh hoạ (tuỳ chọn)')).toBeInTheDocument()
  })

  it('shows thumbnails with 44px remove buttons and locks while sending', async () => {
    let resolve!: (v: unknown) => void
    vi.mocked(api.postForm).mockReturnValue(new Promise(r => { resolve = r }) as never)
    globalThis.URL.createObjectURL = vi.fn(() => 'blob:x')
    globalThis.URL.revokeObjectURL = vi.fn()
    const onSent = vi.fn()
    wrap(<PortalRequestForm projects={[{ id: 'p1', name: 'Tiệm bánh Mây' }]} fixedDeliverable={logo} onSent={onSent} />)

    fireEvent.change(screen.getByLabelText('Ảnh minh hoạ (tuỳ chọn)'), { target: { files: [new File(['x'], 'mau.png', { type: 'image/png' })] } })
    expect(await screen.findByRole('img', { name: 'mau.png' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bỏ ảnh mau.png' })).toHaveClass('h-11', 'w-11')

    fireEvent.change(screen.getByLabelText('Nội dung cần sửa'), { target: { value: 'Đổi màu chữ sang xanh' } })
    fireEvent.click(screen.getByRole('button', { name: 'Gửi yêu cầu' }))
    expect(await screen.findByRole('button', { name: 'Đang gửi 1 ảnh…' })).toBeDisabled()

    resolve({ id: 'r1', projectId: 'p1', projectName: 'Tiệm bánh Mây', deliverableId: 'd1', deliverableName: 'Logo', createdAt: '2026-11-08T03:00:00Z', content: 'Đổi màu chữ sang xanh', status: 'NEW', declineReason: null, attachments: [] })
    await waitFor(() => expect(onSent).toHaveBeenCalledWith(expect.objectContaining({ imageCount: 1, remainingRounds: 2 })))
  })
})

describe('RequestSentSummary (CF-13)', () => {
  it('recaps the request with the expected response time', () => {
    const summary: SentRequestSummary = {
      request: { id: 'r1', projectId: 'p1', projectName: 'Tiệm bánh Mây', deliverableId: 'd1', deliverableName: 'Logo', createdAt: '2026-11-08T03:00:00Z', content: 'Đổi màu chữ sang xanh', status: 'NEW', declineReason: null, attachments: [] },
      imageCount: 2, remainingRounds: 2,
    }
    wrap(<RequestSentSummary summary={summary} deliverableName="Logo" actions={null} />)

    expect(screen.getByText('Đã gửi yêu cầu')).toBeInTheDocument()
    expect(screen.getByText('Đổi màu chữ sang xanh')).toBeInTheDocument()
    expect(screen.getByText('Logo · 2 ảnh đính kèm')).toBeInTheDocument()
    expect(screen.getByText('Nhà thiết kế thường phản hồi trong 24 giờ.')).toBeInTheDocument()
    expect(screen.getByText('Nếu yêu cầu này dùng lượt miễn phí, bạn còn 1 lượt chỉnh sửa miễn phí.')).toBeInTheDocument()
  })
})
