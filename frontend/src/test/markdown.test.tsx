import { render, screen } from '@testing-library/react'
import { MarkdownView } from '@/components/MarkdownView'
import { safeHref } from '@/lib/markdown'

describe('safeHref', () => {
  it.each(['https://pinterest.com/abc', 'http://a.vn/x', 'mailto:lan@a.vn', 'tel:0901234567'])('keeps %s', url => {
    expect(safeHref(url)).toBe(url)
  })

  it.each(['javascript:alert(1)', ' JavaScript:alert(1)', 'data:text/html,<b>x</b>', 'vbscript:x', '/admin', 'không phải link'])(
    'drops %s',
    url => {
      expect(safeHref(url)).toBe('')
    },
  )
})

describe('MarkdownView', () => {
  it('renders formatting and opens safe links in a new tab', () => {
    render(<MarkdownView source={'Xem **moodboard** tại [Pinterest](https://pinterest.com/abc)'} />)

    expect(screen.getByText('moodboard').tagName).toBe('STRONG')
    const link = screen.getByRole('link', { name: 'Pinterest' })
    expect(link).toHaveAttribute('href', 'https://pinterest.com/abc')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('turns bare URLs into links', () => {
    render(<MarkdownView source="Tham khảo https://dribbble.com/x" />)

    expect(screen.getByRole('link', { name: 'https://dribbble.com/x' })).toHaveAttribute('href', 'https://dribbble.com/x')
  })

  it('never renders unsafe links or raw HTML', () => {
    const { container } = render(
      <MarkdownView source={'[Link xấu](javascript:alert(1))\n\n<script>alert(1)</script>\n\nchữ <b>đậm thô</b>'} />,
    )

    expect(screen.queryByRole('link')).toBeNull()
    expect(screen.getByText('Link xấu')).toBeInTheDocument()
    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('b')).toBeNull()
  })
})
