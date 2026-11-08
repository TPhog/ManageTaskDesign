import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { toast } from 'sonner'
import { receivableReminder } from '@/features/admin/dashboard/dashboardUtils'
import { MoneyInsightsView } from '@/features/admin/dashboard/MoneyInsightsBlock'
import type { MoneyInsights, ReceivableItem } from '@/lib/dashboardTypes'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

const receivable: ReceivableItem = {
  quotationId: 'q1', number: 'BG-2030-0003', version: 1, kind: 'INITIAL', clientId: 'c1', clientName: 'Chị Lan',
  projectId: 'p1', projectName: 'Logo ABC', paymentStatus: 'UNPAID', total: 10_000_000, depositPercent: 50,
  depositAmount: 5_000_000, outstanding: 10_000_000, agingFrom: '2030-04-01', agingDays: 44, agingBucket: '31+',
}

const insights = (over: Partial<MoneyInsights['receivables']> = {}): MoneyInsights => ({
  period: 'month', periodStart: '2030-05-01', periodEnd: '2030-05-31',
  current: { start: '2030-05-01', endExclusive: '2030-05-16' },
  previous: { start: '2030-04-01', endExclusive: '2030-04-16' },
  collected: { current: 7_500_000, previous: 5_000_000, deltaPercent: 50, currentDeposits: 5_000_000, currentRemainders: 2_500_000 },
  accepted: { current: 13_300_000, previous: 8_000_000, deltaPercent: 66.3 },
  receivables: {
    total: 10_000_000, items: [receivable],
    byClient: [{ clientId: 'c1', clientName: 'Chị Lan', outstanding: 10_000_000, count: 1 }],
    hiddenItems: 0, ...over,
  },
})

const renderView = (data: MoneyInsights, onPeriodChange = vi.fn(), loading = false) =>
  render(<MemoryRouter><MoneyInsightsView insights={data} period="month" onPeriodChange={onPeriodChange} loading={loading} /></MemoryRouter>)

describe('MoneyInsightsView', () => {
  it('shows collected money with its comparison and the period caption', () => {
    renderView(insights())
    expect(screen.getByTestId('dsh-collected')).toHaveTextContent('7.500.000')
    expect(screen.getByTestId('dsh-collected-delta')).toHaveTextContent('+50,0% so với cùng kỳ trước')
    expect(screen.getByText('Cọc / Phần còn lại')).toBeInTheDocument()
    expect(screen.getByText(/01\/05\/2030/)).toHaveTextContent('15/05/2030')
  })

  it('marks the block busy while the next period loads', () => {
    const { container } = renderView(insights(), vi.fn(), true)
    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true')
  })

  it('switches the period', async () => {
    const onPeriodChange = vi.fn()
    renderView(insights(), onPeriodChange)
    const group = screen.getByRole('group', { name: 'Kỳ báo cáo' })
    expect(within(group).getByRole('button', { name: 'Tháng này' })).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(within(group).getByRole('button', { name: 'Quý này' }))
    expect(onPeriodChange).toHaveBeenCalledWith('quarter')
  })

  it('renders receivables as a real table with a text aging bucket, per-client totals and a QR link', () => {
    renderView(insights())
    expect(screen.getByRole('table', { name: 'Công nợ theo dự án' })).toBeInTheDocument()
    expect(screen.getByText('Quá 30 ngày')).toBeInTheDocument()
    expect(screen.getByTestId('dsh-receivables-total')).toHaveTextContent('10.000.000')
    expect(screen.getByRole('link', { name: /Mở báo giá/ })).toHaveAttribute('href', '/admin/quotations/q1')
    expect(screen.getByText(/Tổng theo khách/)).toBeInTheDocument()
  })

  it('copies the payment reminder for the row', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    renderView(insights())
    await userEvent.click(screen.getByRole('button', { name: /Copy nhắc thanh toán/ }))
    expect(writeText).toHaveBeenCalledWith(receivableReminder(receivable))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã sao chép tin nhắc thanh toán'))
  })

  it('links to the accepted quotations for hidden rows and explains an empty list', () => {
    const { unmount } = renderView(insights({ hiddenItems: 4 }))
    expect(screen.getByRole('link', { name: 'và 4 khoản khác' })).toHaveAttribute('href', '/admin/quotations?status=ACCEPTED')
    unmount()
    renderView(insights({ items: [], byClient: [], total: 0 }))
    expect(screen.getByText('Không có khoản nào đang chờ thu.')).toBeInTheDocument()
  })
})
