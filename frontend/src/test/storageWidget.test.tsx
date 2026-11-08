import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { StorageWidgetView } from '@/features/admin/dashboard/StorageWidget'
import type { StorageUsage } from '@/lib/imageTypes'

const usage = (over: Partial<StorageUsage> = {}): StorageUsage => ({
  usedBytes: 450 * 1024 * 1024, quotaBytes: 500 * 1024 * 1024, percent: 90, level: 'WARNING',
  uploadsBlocked: false, thresholds: [50, 70, 85, 90, 95], ...over,
})

const renderView = (u: StorageUsage) => render(<MemoryRouter><StorageWidgetView usage={u} /></MemoryRouter>)

describe('StorageWidgetView', () => {
  it('shows the percent, a text level, a colored bar and the manage link', () => {
    renderView(usage())
    expect(screen.getByText('90,0%')).toBeInTheDocument()
    expect(screen.getByText('Gần đầy')).toBeInTheDocument()
    const bar = screen.getByRole('progressbar', { name: 'Dung lượng ảnh đã dùng' })
    expect(bar).toHaveAttribute('aria-valuetext', expect.stringContaining('90,0%'))
    expect(bar.firstElementChild).toHaveClass('bg-amber-500')
    expect(screen.getByRole('link', { name: 'Quản lý dung lượng' })).toHaveAttribute('href', '/admin/settings/storage')
  })

  it('warns in text when uploads are blocked', () => {
    renderView(usage({ level: 'BLOCKED', uploadsBlocked: true, percent: 91 }))
    expect(screen.getByText(/Đang chặn tải ảnh mới/)).toBeInTheDocument()
  })
})
