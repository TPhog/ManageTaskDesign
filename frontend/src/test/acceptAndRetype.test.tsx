import type { ReactNode } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { AcceptDialog } from '@/features/admin/quotations/QuotationActionDialogs'
import { EditItemDialog } from '@/features/admin/revisions/EditItemDialog'
import { api } from '@/lib/api'
import type { QuotationDetail } from '@/lib/quotationTypes'
import type { RevisionItem } from '@/lib/revisionTypes'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }))
vi.mock('@/lib/api', () => ({ api: { post: vi.fn(), get: vi.fn(), put: vi.fn() } }))

const wrap = (ui: ReactNode) => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>)
}

const quotation = {
  id: 'q1', projectId: 'p1', number: 'BG-1', rowVersion: 1,
  newDeliverables: [{
    revisionItemId: 'r1', quotationItemId: 'i1', lineDescription: 'Sticker mẫu', serviceId: 's1', serviceName: 'Sticker mẫu',
    createdDeliverableId: null, createdDeliverableName: null, itemDescription: 'Sticker dán ly',
  }],
} as unknown as QuotationDetail

describe('AcceptDialog outcome text (plan 09)', () => {
  it.each([
    ['IN_PROGRESS', 'Sẽ thêm 1 hạng mục mới vào dự án', 'Đang làm'],
    ['DRAFT', 'Sẽ thêm 1 hạng mục mới vào dự án', 'Chưa bắt đầu'],
    ['CANCELLED', '1 hạng mục mới sẽ không được tạo', 'Dự án đã huỷ'],
  ])('project %s', async (status, heading, detail) => {
    vi.mocked(api.get).mockResolvedValue({ id: 'p1', status })
    wrap(<AcceptDialog quotation={quotation} open onClose={() => {}} />)
    expect(await screen.findByText(new RegExp(heading))).toBeInTheDocument()
    expect(screen.getByText(new RegExp(detail))).toBeInTheDocument()
    expect(screen.getByText(/Sticker dán ly/)).toBeInTheDocument()
  })

  it('claims nothing while the project status is unknown', async () => {
    vi.mocked(api.get).mockReturnValue(new Promise(() => {}))
    wrap(<AcceptDialog quotation={quotation} open onClose={() => {}} />)
    expect(await screen.findByText('Đang kiểm tra trạng thái dự án…')).toBeInTheDocument()
    expect(screen.queryByText(/Sẽ thêm/)).not.toBeInTheDocument()
  })
})

describe('EditItemDialog BILLING_LOCKED (BR-48/61)', () => {
  it('toasts the server detail on a 409', async () => {
    const detail = 'Mục đã nằm trong báo giá đã gửi/chấp nhận — không đổi sang/khỏi "Hạng mục mới"'
    vi.mocked(api.put).mockRejectedValue(Object.assign(new Error('x'), { status: 409, code: 'BILLING_LOCKED', detail }))
    const item = {
      id: 'r1', description: 'Sticker', type: 'NEW_DELIVERABLE', billing: 'CHARGEABLE', internalNotes: '', rowVersion: 1,
      quotation: { status: 'SENT' }, tasks: [],
    } as unknown as RevisionItem
    wrap(<EditItemDialog item={item} projectId="p1" deliverableId={null} open onClose={() => {}} />)
    await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
  })
})
