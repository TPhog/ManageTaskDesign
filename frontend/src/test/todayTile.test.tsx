import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { TodayTile } from '@/features/admin/dashboard/TodayTile'

const summary = { newRequests: 2, overdueTasks: 1, dueTodayTasks: 3, awaitingQuotations: 4, expiringSoonQuotations: 0 }

describe('TodayTile (P16)', () => {
  it('shows the five DSH-01 counters as linked stat tiles inside "Cần xử lý"', () => {
    render(<MemoryRouter><TodayTile summary={summary} wide={false} /></MemoryRouter>)
    expect(screen.getByRole('region', { name: 'Hôm nay của bạn' })).toBeInTheDocument()
    const list = screen.getByRole('list', { name: 'Cần xử lý' })
    expect(within(list).getAllByRole('link')).toHaveLength(5)
    expect(within(list).getByTestId('dsh-overdueTasks')).toHaveTextContent('1')
    expect(within(list).getByTestId('dsh-newRequests')).toHaveTextContent('2')
    expect(within(list).getByRole('link', { name: /Yêu cầu mới/ })).toHaveAttribute('href', '/admin/requests')
  })
  it('turns a counter vermilion only when it needs attention', () => {
    render(<MemoryRouter><TodayTile summary={summary} wide /></MemoryRouter>)
    expect(screen.getByRole('link', { name: /Việc quá hạn/ })).toHaveAttribute('data-tone', 'hot')
    expect(screen.getByRole('link', { name: /Báo giá hết hạn trong 3 ngày/ })).toHaveAttribute('data-tone', 'default')
  })
})
