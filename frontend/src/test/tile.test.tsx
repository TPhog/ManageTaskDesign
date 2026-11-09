import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Wallet } from 'lucide-react'
import { Tile, TileHeader } from '@/components/Tile'
import { StatTile } from '@/components/StatTile'
import { PageHeader } from '@/components/PageHeader'

describe('bento patterns (P16)', () => {
  it('Tile is a named region with its tone exposed', () => {
    render(<Tile aria-label="Tiền" tone="inverse"><TileHeader title="Tiền" icon={Wallet} meta="Tháng này" /></Tile>)
    const region = screen.getByRole('region', { name: 'Tiền' })
    expect(region).toHaveAttribute('data-tone', 'inverse')
    expect(region).toHaveClass('tone-inverse')
    expect(screen.getByRole('heading', { level: 2, name: 'Tiền' })).toBeInTheDocument()
  })

  it('StatTile links to the filtered list and keeps the test id on the value', () => {
    render(<MemoryRouter><StatTile to="/admin/tasks?due=overdue" label="Việc quá hạn" value={2} testId="dsh-overdueTasks" tone="hot" /></MemoryRouter>)
    const link = screen.getByRole('link', { name: /Việc quá hạn/ })
    expect(link).toHaveAttribute('href', '/admin/tasks?due=overdue')
    expect(link).toHaveAttribute('data-tone', 'hot')
    expect(screen.getByTestId('dsh-overdueTasks')).toHaveTextContent('2')
  })

  it('PageHeader renders an eyebrow above a rich h1', () => {
    render(<PageHeader eyebrow="Thứ Hai, 09/11/2026" title={<><span className="sr-only">Tổng quan — </span>Chào buổi sáng.</>} />)
    expect(screen.getByText('Thứ Hai, 09/11/2026')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Tổng quan — Chào buổi sáng.')
  })
})
