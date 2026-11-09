import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { CreateRequestDialog } from '@/features/admin/requests/CreateRequestDialog'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
vi.mock('@/lib/pickers', () => ({ useProjectOptions: () => ({ options: [], loading: false }) }))

test('submitting without a project shows the error inline, not as a toast', async () => {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <CreateRequestDialog open onClose={vi.fn()} onCreated={vi.fn()} />
    </QueryClientProvider>,
  )
  await userEvent.click(screen.getByRole('button', { name: 'Ghi nhận' }))
  const alerts = screen.getAllByRole('alert')
  expect(alerts.some(a => a.id === 'request-project-error' && a.textContent === 'Chọn dự án')).toBe(true)
  expect(screen.getByRole('combobox', { name: /Dự án|Chọn dự án/ })).toHaveAttribute('aria-invalid', 'true')
  expect(toast.error).not.toHaveBeenCalled()
})
