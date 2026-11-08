import { portalInviteMessage, portalLinkState } from '@/lib/portalLink'

describe('portalInviteMessage', () => {
  it('greets the client by name and contains the link', () => {
    const msg = portalInviteMessage('Chị Lan', 'http://localhost:5173/p/abc')
    expect(msg).toContain('Chào Chị Lan')
    expect(msg).toContain('http://localhost:5173/p/abc')
  })
})

describe('portalLinkState', () => {
  it('reports a client without a link', () => {
    expect(portalLinkState({ hasPortalLink: false, portalEnabled: false })).toMatchObject({ status: 'NONE', label: 'Chưa có link' })
  })

  it('reports an active link', () => {
    expect(portalLinkState({ hasPortalLink: true, portalEnabled: true })).toMatchObject({ status: 'ACTIVE', label: 'Đang hoạt động' })
  })

  it('reports a link whose portal is switched off', () => {
    expect(portalLinkState({ hasPortalLink: true, portalEnabled: false })).toMatchObject({ status: 'DISABLED', label: 'Đã tắt' })
  })
})
