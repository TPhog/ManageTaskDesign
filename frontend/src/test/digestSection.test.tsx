import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DigestForm, formatHour } from '@/features/admin/settings/DigestSection'

describe('DigestForm', () => {
  it('shows the channel warning when no Discord webhook is configured', () => {
    render(<DigestForm value={{ enabled: true, hourVn: 8, channelConfigured: false }} onSave={vi.fn()} saving={false} />)
    expect(screen.getByText(/Chưa cấu hình Discord webhook/)).toBeInTheDocument()
  })

  it('saves the toggle and hour', async () => {
    const onSave = vi.fn()
    render(<DigestForm value={{ enabled: true, hourVn: 8, channelConfigured: true }} onSave={onSave} saving={false} />)
    await userEvent.selectOptions(screen.getByLabelText('Giờ gửi (giờ Việt Nam)'), '7')
    await userEvent.click(screen.getByRole('switch', { name: 'Gửi nhắc việc mỗi sáng' }))
    await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(onSave).toHaveBeenCalledWith({ enabled: false, hourVn: 7 })
  })

  it('disables the hour while the digest is off', async () => {
    render(<DigestForm value={{ enabled: true, hourVn: 8, channelConfigured: true }} onSave={vi.fn()} saving={false} />)
    await userEvent.click(screen.getByRole('switch', { name: 'Gửi nhắc việc mỗi sáng' }))
    expect(screen.getByLabelText('Giờ gửi (giờ Việt Nam)')).toBeDisabled()
  })

  it('disables Lưu until something changes', () => {
    render(<DigestForm value={{ enabled: true, hourVn: 8, channelConfigured: true }} onSave={vi.fn()} saving={false} />)
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled()
  })
})

describe('formatHour', () => {
  it('pads to HH:00', () => expect(formatHour(8)).toBe('08:00'))
})
