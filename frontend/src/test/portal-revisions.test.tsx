import { render, screen } from '@testing-library/react'
import { formatDate } from '@/lib/format'
import type { PortalRevisions } from '@/lib/portalTypes'
import { hasRounds, roundTitle } from '@/features/portal/revisions'
import { PortalRevisionsView } from '@/features/portal/PortalRevisionsView'

const empty: PortalRevisions = {
  deliverables: [{ id: 'd1', name: 'Logo', includedRounds: 3, usedRounds: 0, remainingRounds: 3, rounds: [] }],
  projectRounds: [],
}

const withRounds: PortalRevisions = {
  deliverables: [{
    id: 'd1', name: 'Logo', includedRounds: 3, usedRounds: 1, remainingRounds: 2,
    rounds: [{
      id: 'r1', roundNo: 1, receivedAt: '2026-11-08',
      items: [
        { id: 'i1', description: 'Đổi màu chữ', type: 'MINOR', billing: 'IN_QUOTA', workStatus: 'IN_PROGRESS' },
        { id: 'i2', description: 'Thêm tagline', type: 'MAJOR', billing: 'CHARGEABLE', workStatus: 'TODO' },
      ],
    }],
  }],
  projectRounds: [{
    id: 'r2', roundNo: 1, receivedAt: '2026-11-08',
    items: [{ id: 'i3', description: 'Sửa lỗi chính tả', type: 'MINOR', billing: 'FREE_FIX', workStatus: 'DONE' }],
  }],
}

describe('roundTitle', () => {
  it('names the round "Lần sửa" with its date (docs/08 §3.3)', () => {
    expect(roundTitle({ roundNo: 2, receivedAt: '2026-11-08' })).toBe(`Lần sửa 2 · ${formatDate('2026-11-08')}`)
  })
})

describe('hasRounds', () => {
  it('is false when no deliverable and no project round has a round', () => {
    expect(hasRounds(empty)).toBe(false)
    expect(hasRounds(withRounds)).toBe(true)
  })
})

describe('PortalRevisionsView', () => {
  it('shows quota, rounds and client wording for billing and status', () => {
    render(<PortalRevisionsView data={withRounds} />)

    expect(screen.getByText('1/3 lần sửa miễn phí')).toBeInTheDocument()
    expect(screen.getAllByText(/^Lần sửa 1 · /)).toHaveLength(2)
    expect(screen.getByText('Trong số lần sửa')).toBeInTheDocument()
    expect(screen.getByText('Tính phí')).toBeInTheDocument()
    expect(screen.getByText('Miễn phí')).toBeInTheDocument()
    expect(screen.getByText('Đang sửa')).toBeInTheDocument()
    expect(screen.getByText('Chung cho dự án')).toBeInTheDocument()
    expect(screen.queryByText('IN_QUOTA')).not.toBeInTheDocument()
  })

  it('says so when there is no round yet', () => {
    render(<PortalRevisionsView data={empty} />)

    expect(screen.getByText('Chưa có lần sửa nào.')).toBeInTheDocument()
  })
})
