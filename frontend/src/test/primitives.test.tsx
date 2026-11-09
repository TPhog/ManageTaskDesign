import { render, screen } from '@testing-library/react'
import { buttonVariants } from '@/components/ui/button'
import { StatusBadge, statusTone } from '@/components/StatusBadge'

describe('button variants (P16)', () => {
  it('has a vermilion highlight variant and a 52px touch size', () => {
    expect(buttonVariants({ variant: 'highlight' })).toContain('bg-highlight')
    expect(buttonVariants({ size: 'touch' })).toContain('h-13')
  })
  it('squares every button under the editorial theme', () => {
    expect(buttonVariants()).toContain('editorial:rounded-none')
  })
})

describe('StatusBadge tones (P16)', () => {
  it('maps every known status to a tone', () => {
    for (const s of ['DRAFT', 'QUOTED', 'IN_PROGRESS', 'ON_HOLD', 'COMPLETED', 'CANCELLED', 'SENT', 'ACCEPTED', 'REJECTED', 'EXPIRED',
      'SUPERSEDED', 'UNPAID', 'DEPOSIT_PAID', 'PAID', 'NEW', 'TRIAGED', 'DECLINED', 'PENDING', 'CONFIRMED', 'ACTIVE', 'INACTIVE',
      'NOT_STARTED', 'IN_REVIEW', 'APPROVED', 'DELIVERED', 'TODO', 'REVIEW', 'DONE', 'BLOCKED']) {
      expect(statusTone[s], s).toBeDefined()
    }
  })
  it('keeps the label and colours by tone, never by colour alone', () => {
    render(<StatusBadge status="IN_REVIEW" />)
    const badge = screen.getByText('Chờ khách duyệt')
    expect(badge).toHaveClass('text-highlight-text')
    render(<StatusBadge status="CANCELLED" />)
    expect(screen.getByText('Đã huỷ')).toHaveClass('text-danger')
  })
})
