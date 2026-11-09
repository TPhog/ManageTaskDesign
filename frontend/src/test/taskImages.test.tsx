import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { TaskImages } from '@/features/admin/tasks/TaskImages'

const mutate = vi.fn()
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }))
vi.mock('@/features/admin/images/useImages', () => ({
  useTaskImages: () => ({ data: [], isLoading: false, isError: false }),
  useUploadTaskImages: () => ({ mutate, isPending: false }),
  useSetImageVisibility: () => ({ mutate: vi.fn() }),
  useDeleteImage: () => ({ mutate: vi.fn(), isPending: false }),
}))

const png = () => new File([new Uint8Array([137, 80, 78, 71])], 'a.png', { type: 'image/png' })

function setup() {
  render(<MemoryRouter><TaskImages taskId="t1" /></MemoryRouter>)
  return screen.getByLabelText('Cho khách xem các ảnh mới tải lên')
}

describe('TaskImages', () => {
  beforeEach(() => mutate.mockClear())

  it('new uploads are private unless the designer opts in', async () => {
    const box = setup()
    expect(box).not.toBeChecked()
    expect(screen.getByText(/chỉ mình bạn thấy/)).toBeInTheDocument()
    await userEvent.upload(document.getElementById('task-images-input') as HTMLInputElement, png())
    expect(mutate.mock.calls[0][0]).toMatchObject({ visibleToClient: false })
  })

  it('ticking the box shares the next uploads', async () => {
    const box = setup()
    await userEvent.click(box)
    await userEvent.upload(document.getElementById('task-images-input') as HTMLInputElement, png())
    expect(mutate.mock.calls[0][0]).toMatchObject({ visibleToClient: true })
  })
})
