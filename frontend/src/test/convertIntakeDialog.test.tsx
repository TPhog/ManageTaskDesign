import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { toast } from 'sonner'
import { ConvertIntakeDialog } from '@/features/admin/intake/ConvertIntakeDialog'
import { api } from '@/lib/api'
import type { IntakeDetail } from '@/lib/requestTemplateTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(async () => []), post: vi.fn() } }))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

const detail: IntakeDetail = {
  id: 'i1', createdAt: '2026-11-13T03:00:00Z', status: 'NEW', contactName: 'Chị Lan', contactPhone: '0909 123 456',
  contactEmail: null, templateName: 'Poster', excerpt: '…', imageCount: 0, templateId: 't1', answers: [], note: '', body: 'Mẫu: Poster',
  images: [], suggestedDesignType: 'POSTER', suggestedServiceId: null, suggestedServiceName: null, suggestedDueDate: null,
  rejectReason: null, rejectedAt: null, convertedClientId: null, convertedProjectId: null, convertedAt: null,
  matchingClients: [{ id: 'c1', name: 'Lan cũ', phone: '0909123456', email: null }], rowVersion: 3,
}

function open(onOpenChange = vi.fn()) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={qc}><MemoryRouter><ConvertIntakeDialog detail={detail} open onOpenChange={onOpenChange} /></MemoryRouter></QueryClientProvider>)
  return onOpenChange
}

describe('ConvertIntakeDialog (review M-6/M-7)', () => {
  beforeEach(() => { vi.mocked(api.post).mockReset(); vi.mocked(toast.error).mockReset() })

  it('says what is missing instead of silently disabling the button', async () => {
    open()
    fireEvent.change(screen.getByLabelText('Tên dự án'), { target: { value: '  ' } })
    fireEvent.change(screen.getByLabelText('Tên khách'), { target: { value: '' } })
    const submit = screen.getByRole('button', { name: 'Tạo khách & dự án' })
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    expect(await screen.findByText('Nhập tên dự án')).toBeInTheDocument()
    expect(screen.getByText('Nhập tên khách')).toBeInTheDocument()
    expect(api.post).not.toHaveBeenCalled()
  })

  it('tells a deleted submission (bodiless 404) apart from an inactive chosen client', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({ status: 404, code: 'NOT_FOUND' })
    const onOpenChange = open()
    fireEvent.click(screen.getByRole('button', { name: 'Tạo khách & dự án' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Khách đã chọn không còn hoạt động'))
    expect(onOpenChange).not.toHaveBeenCalled()

    vi.mocked(api.post).mockRejectedValueOnce({ status: 404 })
    fireEvent.click(screen.getByRole('button', { name: 'Tạo khách & dự án' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Yêu cầu này đã bị xoá'))
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
