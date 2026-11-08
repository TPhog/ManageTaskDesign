import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ProjectHealthView } from '@/features/admin/dashboard/ProjectHealthBlock'
import type { ProjectHealth, ProjectHealthItem } from '@/lib/dashboardTypes'

const item = (over: Partial<ProjectHealthItem>): ProjectHealthItem => ({
  projectId: 'p1', projectName: 'Logo ABC', clientId: 'c1', clientName: 'Khách A', status: 'IN_PROGRESS', stage: 'EXECUTE',
  dueDate: '2030-05-12', daysToDeadline: -3, progressPercent: 40, quotaUsed: 2, quotaTotal: 4,
  risks: ['LATE', 'DEPOSIT_UNPAID'], riskScore: 7, ...over,
})

const renderView = (health: ProjectHealth) =>
  render(<MemoryRouter><ProjectHealthView health={health} /></MemoryRouter>)

describe('ProjectHealthView', () => {
  it('shows risk chips with text, deadline, progress, quota and a link to the project', () => {
    renderView({
      total: 3, hidden: 0,
      items: [
        item({}),
        item({ projectId: 'p2', projectName: 'Web XYZ', daysToDeadline: 1, progressPercent: null, quotaUsed: 0, quotaTotal: 0, risks: [] }),
      ],
    })
    expect(screen.getByText('Trễ hạn')).toBeInTheDocument()
    expect(screen.getByText('Chưa nhận cọc')).toBeInTheDocument()
    expect(screen.getByText('Trễ 3 ngày')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Tiến độ Logo ABC' })).toHaveAttribute('aria-valuenow', '40')
    expect(screen.getByText('2/4 lượt sửa')).toBeInTheDocument()
    expect(screen.getByText('Không có lượt sửa miễn phí')).toBeInTheDocument()
    expect(screen.getByText('Chưa có task')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Logo ABC' })).toHaveAttribute('href', '/admin/projects/p1')
  })

  it('shows the empty state', () => {
    renderView({ total: 0, hidden: 0, items: [] })
    expect(screen.getByText('Chưa có dự án nào đang chạy.')).toBeInTheDocument()
  })

  it('links to the project list for the hidden remainder', () => {
    renderView({ total: 15, hidden: 3, items: [item({})] })
    expect(screen.getByRole('link', { name: 'và 3 dự án khác' })).toHaveAttribute('href', '/admin/projects')
  })
})
