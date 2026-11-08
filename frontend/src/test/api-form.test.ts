import { api } from '@/lib/api'

describe('api.postForm', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.cookie = 'ff_csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
  })

  it('sends FormData as-is with the CSRF header and no JSON content type', async () => {
    document.cookie = 'ff_csrf=abc%2B123; path=/'
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'r1' }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const form = new FormData()
    form.append('content', 'Đổi màu')

    await expect(api.postForm('/portal/projects/p1/requests', form)).resolves.toEqual({ id: 'r1' })

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/portal/projects/p1/requests')
    expect(init.method).toBe('POST')
    expect(init.body).toBe(form)
    expect(init.headers).toEqual({ 'X-CSRF-Token': 'abc+123' })
  })

  it('keeps JSON bodies for post', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await api.post('/portal/logout', {})

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(init.body).toBe('{}')
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json')
  })
})
