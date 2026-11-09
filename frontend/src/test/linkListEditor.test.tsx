import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { LinkChips, LinkListEditor } from '@/features/admin/tasks/LinkListEditor'

describe('LinkListEditor', () => {
  it('adds a link and rejects non-http urls inline', async () => {
    const onChange = vi.fn()
    render(<LinkListEditor value={[]} onChange={onChange} />)
    await userEvent.type(screen.getByLabelText('Địa chỉ liên kết'), 'javascript:alert(1)')
    await userEvent.click(screen.getByRole('button', { name: 'Thêm liên kết' }))
    expect(screen.getByText('Chỉ chấp nhận địa chỉ http hoặc https')).toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()

    await userEvent.clear(screen.getByLabelText('Địa chỉ liên kết'))
    await userEvent.type(screen.getByLabelText('Tên liên kết'), 'Figma')
    await userEvent.type(screen.getByLabelText('Địa chỉ liên kết'), 'https://figma.com/file/1')
    await userEvent.click(screen.getByRole('button', { name: 'Thêm liên kết' }))
    expect(onChange).toHaveBeenCalledWith([{ title: 'Figma', url: 'https://figma.com/file/1', visibleToClient: false }])
  })

  it('shows the client-visible switch only when allowed', () => {
    const v = [{ title: 'A', url: 'https://a.com', visibleToClient: false }]
    const { rerender } = render(<LinkListEditor value={v} onChange={() => {}} />)
    expect(screen.queryByLabelText('Hiện cho khách: A')).toBeNull()
    rerender(<LinkListEditor value={v} onChange={() => {}} allowClientVisible />)
    expect(screen.getByLabelText('Hiện cho khách: A')).toBeInTheDocument()
  })

  it('LinkChips open in a new tab without referrer', () => {
    render(<LinkChips links={[{ title: 'Drive', url: 'https://drive.google.com/x' }]} />)
    const a = screen.getByRole('link', { name: /Drive/ })
    expect(a).toHaveAttribute('target', '_blank')
    expect(a).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
