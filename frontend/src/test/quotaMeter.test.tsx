import { render, screen } from '@testing-library/react'
import { QuotaMeter } from '@/components/QuotaMeter'

test('a deliverable with no free rounds shows a neutral line, not a full red bar', () => {
  const { container } = render(<QuotaMeter used={0} total={0} label="vòng" />)
  expect(screen.getByText('Không có vòng sửa miễn phí')).toBeInTheDocument()
  expect(container.querySelector('.bg-danger')).toBeNull()
  expect(screen.queryByText(/0\/0/)).toBeNull()
})

test('rounds already used are still mentioned', () => {
  render(<QuotaMeter used={2} total={0} />)
  expect(screen.getByText('Không có vòng sửa miễn phí · đã sửa 2 vòng')).toBeInTheDocument()
})

test('with free rounds the usual bar and count stay', () => {
  render(<QuotaMeter used={1} total={3} label="vòng" />)
  expect(screen.getByText('1/3 vòng')).toBeInTheDocument()
})
