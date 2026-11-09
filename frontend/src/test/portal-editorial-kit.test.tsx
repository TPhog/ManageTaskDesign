import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { usePortalTheme } from '@/features/portal/kit/usePortalTheme'
import { splitWordmark } from '@/features/portal/kit/wordmark'
import { Masthead } from '@/features/portal/kit/Masthead'
import { EditorialSection, LeaderList, LeaderRow, LeaderTotal, NumberedList, NumberedListItem } from '@/features/portal/kit/editorial'

function Themed() {
  usePortalTheme()
  return null
}

describe('editorial kit (P16)', () => {
  it('splits a studio name into a plain head and an italic last word', () => {
    expect(splitWordmark('Studio Mây')).toEqual({ head: 'Studio ', tail: 'Mây' })
    expect(splitWordmark('Linh')).toEqual({ head: 'Linh', tail: '' })
    expect(splitWordmark('  Nhà   thiết kế ')).toEqual({ head: 'Nhà thiết ', tail: 'kế' })
  })

  it('puts data-theme="editorial" on <html> while mounted and removes it afterwards', () => {
    const { unmount } = render(<Themed />)
    expect(document.documentElement.dataset.theme).toBe('editorial')
    unmount()
    expect(document.documentElement.dataset.theme).toBeUndefined()
  })

  it('names the section region by its title and hides the numeral from screen readers', () => {
    render(<EditorialSection number="I." title="Việc bạn cần làm"><p>nội dung</p></EditorialSection>)
    const region = screen.getByRole('region', { name: 'Việc bạn cần làm' })
    expect(within(region).getByText('I.')).toHaveAttribute('aria-hidden', 'true')
  })

  it('renders leader rows as a definition list', () => {
    render(
      <LeaderList>
        <LeaderRow label="Báo giá BG-2026-031" value="24.000.000 ₫" />
        <LeaderTotal label="Đợt 2" value="12.000.000 ₫" />
      </LeaderList>,
    )
    expect(screen.getAllByRole('term').map(t => t.textContent)).toEqual(['Báo giá BG-2026-031', 'Đợt 2'])
    expect(screen.getAllByRole('definition').map(d => d.textContent)).toEqual(['24.000.000 ₫', '12.000.000 ₫'])
  })

  it('numbered items link without reading the numeral', () => {
    render(<MemoryRouter><NumberedList><NumberedListItem index={1} title="Duyệt hai hạng mục" meta="Logo chính v2" to="/portal/projects/p1" hot /></NumberedList></MemoryRouter>)
    const link = screen.getByRole('link', { name: /Duyệt hai hạng mục/ })
    expect(link).toHaveAttribute('href', '/portal/projects/p1')
    expect(link).not.toHaveAccessibleName(/01/)
  })

  it('masthead links the wordmark home and shows the subline and actions', () => {
    render(<MemoryRouter><Masthead studioName="Studio Mây" subline="Xin chào, Cà phê Mộc" homeHref="/portal" actions={<button type="button">Thoát</button>} /></MemoryRouter>)
    expect(screen.getByRole('link', { name: 'Studio Mây' })).toHaveAttribute('href', '/portal')
    expect(screen.getByText('Xin chào, Cà phê Mộc')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Thoát' })).toBeInTheDocument()
  })
})
