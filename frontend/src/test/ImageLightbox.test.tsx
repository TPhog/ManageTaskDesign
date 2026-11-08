import { fireEvent, render, screen } from '@testing-library/react'
import { ImageLightbox } from '@/components/ImageLightbox'
import type { LightboxImage } from '@/lib/imageTypes'

const images: LightboxImage[] = [
  { id: '1', src: '/api/portal/images/1/preview', thumbSrc: '/api/portal/images/1/thumb', alt: 'Ảnh 1' },
  { id: '2', src: '/api/portal/images/2/preview', thumbSrc: '/api/portal/images/2/thumb', alt: 'Ảnh 2' },
]

describe('ImageLightbox', () => {
  it('shows the preview, blocks right-click and drag, and pages with the arrows', () => {
    const onIndexChange = vi.fn()
    render(<ImageLightbox images={images} index={0} onIndexChange={onIndexChange} />)

    const img = screen.getByRole('img', { name: 'Ảnh 1' })
    expect(img).toHaveAttribute('src', '/api/portal/images/1/preview')
    expect(img).toHaveAttribute('draggable', 'false')
    expect(fireEvent.contextMenu(img)).toBe(false)   // default prevented
    expect(fireEvent.dragStart(img)).toBe(false)

    expect(screen.getByRole('button', { name: 'Ảnh trước' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Ảnh sau' }))
    expect(onIndexChange).toHaveBeenCalledWith(1)
    fireEvent.keyDown(window, { key: 'ArrowRight' })
    expect(onIndexChange).toHaveBeenCalledTimes(2)
    fireEvent.keyDown(window, { key: 'ArrowLeft' })   // already on the first image: nothing
    expect(onIndexChange).toHaveBeenCalledTimes(2)
  })

  it('renders nothing when closed', () => {
    render(<ImageLightbox images={images} index={null} onIndexChange={() => {}} />)
    expect(screen.queryByRole('img')).toBeNull()
  })
})
