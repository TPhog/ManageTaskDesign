import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { BriefTab } from '@/features/admin/projects/detail/BriefTab'
import type { Brief } from '@/lib/types'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

const brief = (over: Partial<Brief>): Brief => ({
  id: 'b1', version: 1, status: 'DRAFT', content: '', confirmedAt: null, createdAt: '2026-11-01T00:00:00Z', updatedAt: '2026-11-01T00:00:00Z',
  ...over,
}) as Brief

const renderTab = (b: Brief) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <BriefTab projectId="p1" brief={b} />
    </QueryClientProvider>,
  )

test('a confirmed brief is rendered as Markdown', () => {
  renderTab(brief({ status: 'CONFIRMED', content: '## Mục tiêu\n**đậm**', confirmedAt: '2026-11-02T00:00:00Z' }))
  expect(screen.getByRole('heading', { name: 'Mục tiêu' })).toBeInTheDocument()
  expect(screen.getByText('đậm').tagName).toBe('STRONG')
})

test('a draft brief uses the Markdown editor with Xem / Sửa', async () => {
  renderTab(brief({ content: '' }))
  expect(screen.getByRole('group', { name: /Chế độ nội dung brief/i })).toBeInTheDocument()
  await userEvent.type(screen.getByLabelText('Nội dung brief'), '**mạnh**')
  await userEvent.click(screen.getByRole('button', { name: 'Xem' }))
  expect(screen.getByText('mạnh').tagName).toBe('STRONG')
})
