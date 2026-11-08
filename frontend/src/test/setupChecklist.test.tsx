import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { SetupChecklistView } from '@/features/admin/dashboard/SetupChecklistCard'
import type { SetupChecklist } from '@/lib/dashboardTypes'

const checklist: SetupChecklist = {
  dismissed: false,
  complete: false,
  doneCount: 1,
  total: 6,
  items: [
    { key: 'STUDIO', label: 'Điền thông tin studio', hint: 'Tên studio hiện trên báo giá', done: true, href: '/admin/settings' },
    { key: 'BANK', label: 'Nhập tài khoản ngân hàng', hint: 'Để tạo mã VietQR', done: false, href: '/admin/settings' },
    { key: 'SERVICE', label: 'Xem lại bảng giá dịch vụ', hint: 'Giá mẫu', done: false, href: '/admin/services' },
  ],
}

describe('SetupChecklistView', () => {
  it('lists the items with progress, links each open one to its page and offers dismiss', async () => {
    const onDismiss = vi.fn()
    render(<MemoryRouter><SetupChecklistView checklist={checklist} onDismiss={onDismiss} dismissing={false} /></MemoryRouter>)
    expect(screen.getByText('1/6')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Tiến độ thiết lập' })).toHaveAttribute('aria-valuenow', '1')
    expect(screen.getByRole('link', { name: /Nhập tài khoản ngân hàng/ })).toHaveAttribute('href', '/admin/settings')
    expect(screen.queryByRole('link', { name: /Điền thông tin studio/ })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ẩn hướng dẫn' }))
    expect(onDismiss).toHaveBeenCalled()
  })
})
