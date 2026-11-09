import { render, screen } from '@testing-library/react'
import { PortalRequestList } from '@/features/portal/PortalRequestList'
import type { PortalRequest } from '@/lib/portalTypes'

function request(overrides: Partial<PortalRequest> = {}): PortalRequest {
  return {
    id: 'r1', projectId: 'p1', projectName: 'Nhận diện ABC', deliverableId: 'd1', deliverableName: 'Logo',
    createdAt: '2026-11-08T03:00:00Z', content: 'Đổi màu chữ sang xanh navy', status: 'NEW',
    declineReason: null, attachments: [], ...overrides,
  }
}

describe('PortalRequestList', () => {
  it('shows content, deliverable and the client-facing status', () => {
    render(<PortalRequestList items={[request()]} />)

    const item = screen.getByRole('listitem')
    expect(item).toHaveTextContent('Đổi màu chữ sang xanh navy')
    expect(item).toHaveTextContent('Logo')
    expect(item).toHaveTextContent('Đã gửi — chờ nhà thiết kế xem')
    expect(item).not.toHaveTextContent('Nhận diện ABC')
  })

  it('shows the decline reason and the project when asked', () => {
    render(
      <PortalRequestList
        items={[request({ status: 'DECLINED', declineReason: 'Ngoài phạm vi hợp đồng' })]}
        showProject
      />,
    )

    const item = screen.getByRole('listitem')
    expect(item).toHaveTextContent('Nhà thiết kế không nhận yêu cầu này')
    expect(item).toHaveTextContent('Lý do: Ngoài phạm vi hợp đồng')
    expect(item).toHaveTextContent('Nhận diện ABC')
  })
})
