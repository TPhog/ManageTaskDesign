import { fireEvent, render, screen } from '@testing-library/react'
import { OriginalCleanupDialog } from '@/features/admin/settings/OriginalCleanupDialog'
import type { OriginalCleanupPreview } from '@/lib/imageTypes'

const preview: OriginalCleanupPreview = {
  olderThanMonths: 6, cutoff: '2026-05-12T03:00:00Z', projectCount: 2, imageCount: 30, freedBytes: 40 * 1024 ** 2,
  digest: 'abc', projects: [],
}

describe('OriginalCleanupDialog', () => {
  it('needs the irreversible checkbox before it confirms', () => {
    const onConfirm = vi.fn()
    render(<OriginalCleanupDialog open onOpenChange={() => {}} preview={preview} pending={false} onConfirm={onConfirm} />)

    expect(screen.getByRole('dialog', { name: 'Xoá bản gốc của 30 ảnh?' })).toBeInTheDocument()
    expect(screen.getByText(/Giải phóng 40,0 MB ở 2 dự án/)).toBeInTheDocument()
    const confirm = screen.getByRole('button', { name: 'Xoá vĩnh viễn bản gốc' })
    expect(confirm).toBeDisabled()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Tôi hiểu bản gốc sẽ bị xoá vĩnh viễn' }))
    expect(confirm).toBeEnabled()
    fireEvent.click(confirm)
    expect(onConfirm).toHaveBeenCalledTimes(1)
  })
})
