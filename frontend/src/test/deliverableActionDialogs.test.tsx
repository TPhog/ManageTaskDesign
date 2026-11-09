import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApproveDialog, DeliverDialog, SubmitReviewDialog } from '@/features/admin/flow/DeliverableActionDialogs'

const d = { id: 'd1', name: 'Logo' }

describe('SubmitReviewDialog', () => {
  const base = { deliverable: d, open: true, onOpenChange: vi.fn(), pending: false }

  it('warns about unfinished tasks without blocking', async () => {
    const onConfirm = vi.fn()
    render(<SubmitReviewDialog {...base} openTasks={{ total: 2, shown: ['Moodboard', 'Phác thảo'], more: 0 }} onConfirm={onConfirm} />)
    expect(screen.getByText(/Còn 2 công việc chưa xong/)).toHaveTextContent('Moodboard, Phác thảo')
    await userEvent.click(screen.getByRole('button', { name: 'Vẫn gửi duyệt' }))
    expect(onConfirm).toHaveBeenCalledWith(null)
  })

  it('has no warning block when every task is done', async () => {
    const onConfirm = vi.fn()
    render(<SubmitReviewDialog {...base} openTasks={{ total: 0, shown: [], more: 0 }} onConfirm={onConfirm} />)
    expect(screen.queryByText(/công việc chưa xong/)).toBeNull()
    await userEvent.type(screen.getByLabelText('Lời nhắn cho khách (tuỳ chọn)'), 'Mời duyệt')
    await userEvent.click(screen.getByRole('button', { name: 'Gửi duyệt' }))
    expect(onConfirm).toHaveBeenCalledWith('Mời duyệt')
  })
})

describe('DeliverDialog', () => {
  const setup = () => {
    const onConfirm = vi.fn()
    render(<DeliverDialog deliverable={d} open onOpenChange={vi.fn()} pending={false} onConfirm={onConfirm} />)
    return onConfirm
  }

  it('rejects a non-http link inline and does not confirm', async () => {
    const onConfirm = setup()
    await userEvent.type(screen.getByLabelText('Link file bàn giao (tuỳ chọn)'), 'javascript:x')
    await userEvent.click(screen.getByRole('button', { name: 'Bàn giao' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Link phải bắt đầu bằng http:// hoặc https://')
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('sends the link and the message', async () => {
    const onConfirm = setup()
    await userEvent.type(screen.getByLabelText('Link file bàn giao (tuỳ chọn)'), 'https://drive.google.com/x')
    await userEvent.type(screen.getByLabelText('Lời nhắn cho khách (tuỳ chọn)'), 'Bản final')
    await userEvent.click(screen.getByRole('button', { name: 'Bàn giao' }))
    expect(onConfirm).toHaveBeenCalledWith({ deliveryUrl: 'https://drive.google.com/x', clientMessage: 'Bản final' })
  })

  it('both fields are optional', async () => {
    const onConfirm = setup()
    await userEvent.click(screen.getByRole('button', { name: 'Bàn giao' }))
    expect(onConfirm).toHaveBeenCalledWith({ deliveryUrl: null, clientMessage: null })
  })
})

describe('ApproveDialog', () => {
  it('says it records on behalf of the client unless the portal already approved', () => {
    const { rerender } = render(<ApproveDialog deliverable={{ ...d, clientDecision: null }} open onOpenChange={vi.fn()} pending={false} onConfirm={vi.fn()} />)
    expect(screen.getByText(/hộ khách/)).toBeInTheDocument()
    rerender(<ApproveDialog deliverable={{ ...d, clientDecision: 'APPROVED' }} open onOpenChange={vi.fn()} pending={false} onConfirm={vi.fn()} />)
    expect(screen.getByText(/đã duyệt trên portal/)).toBeInTheDocument()
  })
})
