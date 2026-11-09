import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import SettingsPage from '@/features/admin/settings/SettingsPage'
import { parseSettingsTab } from '@/features/admin/settings/settingsTabs'
import { buildSettingsPayload, CONFLICT_MESSAGE, type Settings } from '@/features/admin/settings/settingsForm'
import { api } from '@/lib/api'

vi.mock('@/lib/api', async importOriginal => ({
  ...(await importOriginal<typeof import('@/lib/api')>()),
  api: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
import { toast } from 'sonner'

const base: Settings = {
  displayName: 'CamCam Studio',
  contactEmail: 'cam@example.com',
  contactPhone: null,
  logoFileId: null,
  bankBin: '970436',
  bankAccountNo: '0123456789',
  bankAccountName: 'NGUYEN VAN A',
  quotationPrefix: 'BG',
  defaultValidityDays: 30,
  defaultDepositPercent: 50,
  defaultTerms: '',
  portalResponseTime: 'trong 24 giờ',
  rowVersion: 7,
}

let server: Settings

function renderPage(path = '/admin/settings') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/admin/settings', element: <SettingsPage /> },
      { path: '/admin', element: <p>Tổng quan</p> },
    ],
    { initialEntries: [path] },
  )
  render(<QueryClientProvider client={qc}><RouterProvider router={router} /></QueryClientProvider>)
  return router
}

beforeEach(() => {
  vi.clearAllMocks()
  server = { ...base }
  vi.mocked(api.get).mockImplementation(async (path: string) => {
    if (path === '/admin/settings') return { ...server }
    if (path === '/admin/banks') return [{ bin: '970436', code: 'VCB', shortName: 'Vietcombank', name: 'Ngoại thương' }]
    throw new Error(`unexpected GET ${path}`)
  })
})

describe('parseSettingsTab', () => {
  it('maps unknown or missing values to studio', () => {
    expect(parseSettingsTab(null)).toBe('studio')
    expect(parseSettingsTab('nope')).toBe('studio')
    expect(parseSettingsTab('payment')).toBe('payment')
  })
})

describe('buildSettingsPayload', () => {
  it('keeps every other section from the server and only overrides the given fields', () => {
    const body = buildSettingsPayload(base, { bankAccountNo: '999988887777' })
    expect(body).toMatchObject({
      displayName: 'CamCam Studio', contactEmail: 'cam@example.com', quotationPrefix: 'BG',
      bankBin: '970436', bankAccountNo: '999988887777', portalResponseTime: 'trong 24 giờ', rowVersion: 7,
    })
  })

  it('sends blank inputs as null but a blank response time as an empty string', () => {
    const body = buildSettingsPayload(base, { bankBin: '', bankAccountNo: '', bankAccountName: '', portalResponseTime: '   ' })
    expect(body.bankBin).toBeNull()
    expect(body.bankAccountNo).toBeNull()
    expect(body.bankAccountName).toBeNull()
    expect(body.portalResponseTime).toBe('')
  })

  it('sends emptied default terms as an empty string so the API clears them (null would keep the old text)', () => {
    expect(buildSettingsPayload({ ...base, defaultTerms: 'Cũ' }, { defaultTerms: '' }).defaultTerms).toBe('')
    expect(buildSettingsPayload({ ...base, defaultTerms: 'Cũ' }, {}).defaultTerms).toBe('Cũ')
  })
})

describe('SettingsPage tabs', () => {
  it('opens the tab named in ?tab= and defaults to Studio', async () => {
    renderPage('/admin/settings?tab=payment')
    expect(await screen.findByLabelText('Số tài khoản')).toBeInTheDocument()
    expect(screen.queryByLabelText('Tên hiển thị')).not.toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Thanh toán' })).toHaveAttribute('aria-selected', 'true')
  })

  it('keeps the active tab in the URL', async () => {
    const router = renderPage()
    expect(await screen.findByLabelText('Tên hiển thị')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('tab', { name: 'Báo giá' }))
    expect(await screen.findByLabelText('Tiền tố số BG')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?tab=quotation')
    expect(router.state.historyAction).toBe('REPLACE')
  })
})

describe('SettingsPage per-section save', () => {
  it('Save stays disabled until the section is edited', async () => {
    renderPage('/admin/settings?tab=payment')
    const save = await screen.findByRole('button', { name: 'Lưu tài khoản' })
    expect(save).toBeDisabled()
    await userEvent.type(screen.getByLabelText('Số tài khoản'), '1')
    expect(save).toBeEnabled()
  })

  it('saving Payment sends the latest Studio values from the server', async () => {
    vi.mocked(api.put).mockImplementation(async (_path, body) => {
      const b = body as Settings
      server = { ...server, ...b, rowVersion: server.rowVersion + 1 }
      return undefined
    })
    renderPage()
    const name = await screen.findByLabelText('Tên hiển thị')
    await userEvent.clear(name)
    await userEvent.type(name, 'Studio Mới')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã lưu hồ sơ'))
    await waitFor(() => expect(vi.mocked(api.get).mock.calls.filter(c => c[0] === '/admin/settings')).toHaveLength(2))

    await userEvent.click(screen.getByRole('tab', { name: 'Thanh toán' }))
    const acc = await screen.findByLabelText('Số tài khoản')
    await userEvent.clear(acc)
    await userEvent.type(acc, '55556666')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu tài khoản' }))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã lưu tài khoản ngân hàng'))

    const second = vi.mocked(api.put).mock.calls[1][1] as Record<string, unknown>
    expect(second).toMatchObject({ displayName: 'Studio Mới', bankAccountNo: '55556666', bankBin: '970436', rowVersion: 8 })
  })

  it('shows API field errors next to the field', async () => {
    vi.mocked(api.put).mockRejectedValue(Object.assign(new Error('bad'), { status: 400, errors: { bankAccountNo: ['Sai định dạng'] } }))
    renderPage('/admin/settings?tab=payment')
    await userEvent.type(await screen.findByLabelText('Số tài khoản'), '9')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu tài khoản' }))
    expect(await screen.findByText('Sai định dạng')).toBeInTheDocument()
  })

  it('on 409 reloads the settings and resets the section to the server values', async () => {
    vi.mocked(api.put).mockImplementation(async () => {
      server = { ...server, displayName: 'Đổi ở tab khác', rowVersion: 9 }
      throw Object.assign(new Error('conflict'), { status: 409, code: 'CONCURRENCY_CONFLICT' })
    })
    renderPage()
    const name = await screen.findByLabelText('Tên hiển thị')
    await userEvent.type(name, ' X')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(CONFLICT_MESSAGE))
    await waitFor(() => expect(screen.getByLabelText('Tên hiển thị')).toHaveValue('Đổi ở tab khác'))
    expect(screen.getByRole('button', { name: 'Lưu hồ sơ' })).toBeDisabled()
  })
})

describe('SettingsPage per-section save (continued)', () => {
  it('a section save keeps the saved logo', async () => {
    server = { ...base, logoFileId: 'logo-1' }
    vi.mocked(api.put).mockResolvedValue(undefined)
    renderPage()
    await userEvent.type(await screen.findByLabelText('Tên hiển thị'), ' 3')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1))
    expect(vi.mocked(api.put).mock.calls[0][1]).toMatchObject({ logoFileId: 'logo-1', displayName: 'CamCam Studio 3' })
  })

  it('after a 409 the next save uses the refreshed rowVersion', async () => {
    vi.mocked(api.put)
      .mockImplementationOnce(async () => {
        server = { ...server, displayName: 'Đổi ở tab khác', rowVersion: 9 }
        throw Object.assign(new Error('conflict'), { status: 409, code: 'CONCURRENCY_CONFLICT' })
      })
      .mockResolvedValueOnce(undefined)
    renderPage()
    await userEvent.type(await screen.findByLabelText('Tên hiển thị'), ' X')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(screen.getByLabelText('Tên hiển thị')).toHaveValue('Đổi ở tab khác'))

    await userEvent.type(screen.getByLabelText('Tên hiển thị'), ' Y')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(2))
    expect(vi.mocked(api.put).mock.calls[0][1]).toMatchObject({ rowVersion: 7 })
    expect(vi.mocked(api.put).mock.calls[1][1]).toMatchObject({ rowVersion: 9, displayName: 'Đổi ở tab khác Y' })
  })
})

describe('SettingsPage unsaved-changes guard', () => {
  const unloadBlocked = () => {
    const e = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(e)
    return e.defaultPrevented
  }

  it('blocks closing the browser tab only while a section is dirty', async () => {
    vi.mocked(api.put).mockResolvedValue(undefined)
    renderPage()
    const name = await screen.findByLabelText('Tên hiển thị')
    expect(unloadBlocked()).toBe(false)
    await userEvent.type(name, ' 4')
    expect(unloadBlocked()).toBe(true)
    await userEvent.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }))
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã lưu hồ sơ'))
    await waitFor(() => expect(unloadBlocked()).toBe(false))
  })

  it('arrow keys only move focus along the tabs; Enter opens one (and asks first when dirty)', async () => {
    const router = renderPage()
    await userEvent.type(await screen.findByLabelText('Tên hiển thị'), ' 5')
    screen.getByRole('tab', { name: 'Studio' }).focus()
    await userEvent.keyboard('{ArrowRight}')
    expect(screen.getByRole('tab', { name: 'Thanh toán' })).toHaveFocus()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(router.state.location.search).toBe('')
    // keydown only: user-event's synthetic Enter click would land on the dialog's focused button (the e2e spec covers a real browser)
    fireEvent.keyDown(screen.getByRole('tab', { name: 'Thanh toán' }), { key: 'Enter' })
    expect(await screen.findByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toBeInTheDocument()
  })

  it('asks before switching tab with unsaved edits, and can keep or drop them', async () => {
    const router = renderPage()
    const name = await screen.findByLabelText('Tên hiển thị')
    await userEvent.type(name, ' 2')

    await userEvent.click(screen.getByRole('tab', { name: 'Thanh toán' }))
    expect(await screen.findByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Tiếp tục sửa' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByLabelText('Tên hiển thị')).toHaveValue('CamCam Studio 2')
    expect(router.state.location.search).toBe('')

    await userEvent.click(screen.getByRole('tab', { name: 'Thanh toán' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Bỏ thay đổi' }))
    expect(await screen.findByLabelText('Số tài khoản')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?tab=payment')
  })

  it('also guards leaving the page, and lets clean tabs switch freely', async () => {
    const router = renderPage()
    await screen.findByLabelText('Tên hiển thị')
    await userEvent.click(screen.getByRole('tab', { name: 'Thanh toán' }))
    expect(await screen.findByLabelText('Số tài khoản')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Số tài khoản'), '1')
    await router.navigate('/admin')
    expect(await screen.findByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Bỏ thay đổi' }))
    expect(await screen.findByText('Tổng quan')).toBeInTheDocument()
  })
})
