import { render, screen } from '@testing-library/react'
import { Pagination } from '@/components/Pagination'

describe('Pagination', () => {
  it('falls back to the last page when the current page no longer exists', () => {
    const onPageChange = vi.fn()
    render(<Pagination page={3} pageSize={20} total={25} onPageChange={onPageChange} itemLabel="khách" />)
    expect(onPageChange).toHaveBeenCalledWith(2)
  })

  it('goes back to page 1 when nothing is left', () => {
    const onPageChange = vi.fn()
    render(<Pagination page={2} pageSize={20} total={0} onPageChange={onPageChange} itemLabel="khách" />)
    expect(onPageChange).toHaveBeenCalledWith(1)
  })

  it('stays hidden on page 1 when everything fits', () => {
    render(<Pagination page={1} pageSize={20} total={5} onPageChange={() => {}} itemLabel="khách" />)
    expect(screen.queryByRole('navigation')).toBeNull()
  })

  it('stays visible past page 1 so the user can go back', () => {
    render(<Pagination page={2} pageSize={20} total={15} onPageChange={() => {}} itemLabel="khách" />)
    expect(screen.getByRole('navigation')).toBeInTheDocument()
  })
})
