import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useSendPortalRequest } from '@/features/portal/usePortal'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), postForm: vi.fn() } }))

describe('useSendPortalRequest (review M-1)', () => {
  it('refreshes the requests and every project query (home list, project page) after sending', async () => {
    vi.mocked(api.postForm).mockResolvedValue({ id: 'r1' } as never)
    const qc = new QueryClient()
    const spy = vi.spyOn(qc, 'invalidateQueries')
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    const { result } = renderHook(() => useSendPortalRequest(), { wrapper })

    result.current.mutate({ projectId: 'p1', deliverableId: 'd1', content: 'Đổi màu', files: [] } as never)

    await waitFor(() => expect(spy).toHaveBeenCalledWith({ queryKey: qk.portal.projects }))
    expect(spy).toHaveBeenCalledWith({ queryKey: ['portal', 'requests'] })
  })
})
