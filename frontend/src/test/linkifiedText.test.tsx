import { render, screen } from '@testing-library/react'
import { LinkifiedText } from '@/components/LinkifiedText'

test('an http(s) URL becomes a safe link without its trailing punctuation', () => {
  render(<LinkifiedText text="Bản final: https://drive.google.com/x." />)
  const link = screen.getByRole('link')
  expect(link).toHaveAttribute('href', 'https://drive.google.com/x')
  expect(link.getAttribute('rel')).toContain('noopener')
  expect(link).toHaveAttribute('target', '_blank')
})

test('dangerous schemes and markup stay plain text', () => {
  const { container } = render(<LinkifiedText text={'javascript:alert(1)\n<script>x</script>'} />)
  expect(screen.queryByRole('link')).toBeNull()
  expect(container.querySelector('script')).toBeNull()
  expect(container).toHaveTextContent('<script>x</script>')
})

test('several links in one message', () => {
  render(<LinkifiedText text="A https://a.example/1, B (https://b.example/2)" />)
  expect(screen.getAllByRole('link').map(a => a.getAttribute('href'))).toEqual(['https://a.example/1', 'https://b.example/2'])
})
