import { fireEvent, render, screen } from '@testing-library/react'
import { PortalTimeline } from '@/features/portal/PortalTimeline'
import { linkify } from '@/features/portal/linkify'
import type { PortalTimelineItem } from '@/lib/portalTypes'

const items: PortalTimelineItem[] = Array.from({ length: 8 }, (_, i) => ({
  occurredAt: new Date(Date.UTC(2026, 10, 8, 3, 59 - i)).toISOString(), message: `Sự kiện ${i + 1}`, kind: 'EVENT', images: [],
}))

describe('PortalTimeline (CF-15)', () => {
  it('shows the 5 newest and expands on demand', () => {
    render(<PortalTimeline items={items} />)
    expect(screen.getByText('Sự kiện 5')).toBeInTheDocument()
    expect(screen.queryByText('Sự kiện 6')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xem thêm 3 hoạt động' }))
    expect(screen.getByText('Sự kiện 8')).toBeInTheDocument()
  })
})

describe('linkify (delivery links in the timeline)', () => {
  it('turns only http(s) URLs into safe links and keeps the rest as text', () => {
    render(<p data-testid="msg">{linkify('Bản giao: https://drive.google.com/x. Hoặc javascript:alert(1) và ftp://a.b')}</p>)
    const link = screen.getByRole('link', { name: 'https://drive.google.com/x' })
    expect(link).toHaveAttribute('href', 'https://drive.google.com/x')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    expect(link).toHaveAttribute('target', '_blank')
    expect(screen.getAllByRole('link')).toHaveLength(1)
    expect(screen.getByTestId('msg')).toHaveTextContent('Bản giao: https://drive.google.com/x. Hoặc javascript:alert(1) và ftp://a.b')
  })

  it('keeps line breaks of a delivery message', () => {
    render(<PortalTimeline items={[{ occurredAt: '2026-11-08T03:00:00Z', message: 'Dòng 1' + String.fromCharCode(10) + 'https://a.example/b', kind: 'MESSAGE', images: [] }]} />)
    const quote = screen.getByRole('link', { name: 'https://a.example/b' }).closest('blockquote')
    expect(quote).toHaveClass('whitespace-pre-wrap', 'break-words')
  })
})
