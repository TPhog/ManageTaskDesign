import { render, screen } from '@testing-library/react'
import { PriorityBadge } from '@/features/admin/tasks/TaskMeta'

test('Medium priority renders nothing by default', () => {
  const { container } = render(<PriorityBadge priority="MEDIUM" />)
  expect(container).toBeEmptyDOMElement()
})

test('High priority shows its label', () => {
  render(<PriorityBadge priority="HIGH" />)
  expect(screen.getByText('Cao')).toBeInTheDocument()
})

test('alwaysShow keeps Medium visible (table column)', () => {
  render(<PriorityBadge priority="MEDIUM" alwaysShow />)
  expect(screen.getByText('Trung bình')).toBeInTheDocument()
})
