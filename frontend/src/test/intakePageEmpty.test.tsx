import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import IntakePage from '@/features/admin/intake/IntakePage'

vi.mock('@/features/admin/intake/useIntake', () => ({
  INTAKE_PAGE_SIZE: 20,
  useIntakeList: () => ({ data: { items: [], total: 0 }, isLoading: false, error: null, refetch: vi.fn() }),
}))
vi.mock('@/features/admin/intake/IntakeSheet', () => ({ IntakeSheet: () => null }))

it('empty inbox links to the settings tab that holds the new-client form', () => {
  render(<MemoryRouter initialEntries={['/admin/intake']}><Routes><Route path="/admin/intake" element={<IntakePage />} /></Routes></MemoryRouter>)
  expect(screen.getByRole('link', { name: 'Mở Cài đặt' })).toHaveAttribute('href', '/admin/settings?tab=portal')
})
