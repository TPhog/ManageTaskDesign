import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PortalPaymentBlock } from '@/features/portal/PortalPaymentBlock'
import { api } from '@/lib/api'
import type { PortalQuotationDetail } from '@/lib/portalTypes'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

const q = {
  id: 'q1', projectId: 'p1', status: 'SENT', effectiveStatus: 'SENT', paymentStatus: 'UNPAID', remainingAmount: 3_510_000, clientAcceptedAt: null,
  clientPaymentNotice: null, versions: [],
  snapshot: { number: 'BG-2026-0001', total: 7_020_000, depositAmount: 3_510_000, depositPercent: 50,
    bank: { bin: '970436', shortName: 'Vietcombank', accountNo: '0123456789', accountName: 'NGUYEN MINH ANH' } },
  qrOptions: [
    { purpose: 'deposit', label: 'Thanh toán cọc 50%', amount: 3_510_000, memo: 'BG20260001 COC', payload: '000201' },
    { purpose: 'full', label: 'Thanh toán toàn bộ', amount: 7_020_000, memo: 'BG20260001 TT', payload: '000202' },
  ],
} as unknown as PortalQuotationDetail

const renderBlock = (over: Partial<PortalQuotationDetail> = {}) => render(
  <QueryClientProvider client={new QueryClient()}><PortalPaymentBlock quotation={{ ...q, ...over }} /></QueryClientProvider>)

describe('PortalPaymentBlock (CF-04/05)', () => {
  it('says what to pay now and later, with copy buttons on every line', () => {
    renderBlock()
    expect(screen.getByTestId('pay-now')).toHaveTextContent('3.510.000')
    expect(screen.getByTestId('pay-later')).toHaveTextContent('Còn lại')
    for (const what of ['tên ngân hàng', 'số tài khoản', 'chủ tài khoản', 'số tiền', 'nội dung chuyển khoản'])
      expect(screen.getByRole('button', { name: `Sao chép ${what}` })).toHaveClass('h-11', 'w-11')
    expect(screen.getByText('Mã QR đã có sẵn số tiền và nội dung — bạn không cần nhập tay.')).toBeInTheDocument()
    expect(screen.getByText(/Nội dung “BG20260001 COC” giúp nhà thiết kế biết đây là tiền cọc/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Lưu ảnh QR' })).toHaveAttribute('href', '/api/portal/quotations/q1/qr/deposit.png')
    expect(screen.getByText(/Mở app ngân hàng → Quét QR → chọn ảnh vừa lưu/)).toBeInTheDocument()
  })

  it('switches the amount when the client picks "Chuyển toàn bộ"', () => {
    renderBlock()
    fireEvent.click(screen.getByRole('radio', { name: /Chuyển toàn bộ một lần/ }))
    expect(screen.getByTestId('qr-amount')).toHaveTextContent('7.020.000')
    expect(screen.getByRole('link', { name: 'Lưu ảnh QR' })).toHaveAttribute('href', '/api/portal/quotations/q1/qr/full.png')
  })

  it('reports the transfer with an optional note, then shows it is waiting for the designer', async () => {
    vi.mocked(api.post).mockResolvedValue({ ...q, clientPaymentNotice: { purpose: 'deposit', amount: 3_510_000, at: '2026-11-08T03:00:00Z', note: 'TK chồng', pending: true } })
    renderBlock()

    fireEvent.click(screen.getByRole('button', { name: 'Tôi đã chuyển khoản' }))
    fireEvent.change(screen.getByLabelText('Ghi chú cho nhà thiết kế (tuỳ chọn)'), { target: { value: 'TK chồng' } })
    fireEvent.click(screen.getByRole('button', { name: 'Báo đã chuyển' }))

    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/portal/quotations/q1/payment-notice', { purpose: 'deposit', note: 'TK chồng' }))
  })

  it('moves between the amounts with the arrow keys, one Tab stop (ARIA radio group)', () => {
    renderBlock()
    const deposit = screen.getByRole('radio', { name: /Chuyển tiền cọc/ })
    const full = screen.getByRole('radio', { name: /Chuyển toàn bộ một lần/ })
    expect(deposit).toHaveAttribute('tabindex', '0')
    expect(full).toHaveAttribute('tabindex', '-1')
    fireEvent.keyDown(deposit, { key: 'ArrowDown' })
    expect(full).toHaveAttribute('aria-checked', 'true')
    expect(full).toHaveFocus()
    expect(screen.getByTestId('qr-amount')).toHaveTextContent('7.020.000')
    fireEvent.keyDown(full, { key: 'ArrowDown' })
    expect(deposit).toHaveAttribute('aria-checked', 'true')
  })

  it('asks for nothing on an expired quotation', () => {
    renderBlock({ effectiveStatus: 'EXPIRED' })
    expect(screen.getByText('Báo giá đã hết hạn, vui lòng liên hệ nhà thiết kế trước khi chuyển tiền.')).toBeInTheDocument()
    expect(screen.queryByTestId('pay-now')).not.toBeInTheDocument()
    expect(screen.queryByTestId('vietqr')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tôi đã chuyển khoản' })).not.toBeInTheDocument()
  })

  it('hides the report button while a notice for this amount is pending', () => {
    renderBlock({ clientPaymentNotice: { purpose: 'deposit', amount: 3_510_000, at: '2026-11-08T03:00:00Z', note: null, pending: true } })
    expect(screen.queryByRole('button', { name: 'Tôi đã chuyển khoản' })).not.toBeInTheDocument()
    expect(screen.getByText(/Bạn đã báo chuyển/)).toBeInTheDocument()
  })
})
