import { fireEvent, render, screen } from '@testing-library/react'
import { PortalLightbox } from '@/features/portal/PortalLightbox'
import type { LightboxImage } from '@/lib/imageTypes'

const images: LightboxImage[] = [
  { id: '1', src: '/api/portal/images/1/preview', thumbSrc: '/api/portal/images/1/thumb', alt: 'Ảnh Logo', caption: 'Logo · 08/11/2026' },
  { id: '2', src: '/api/portal/images/2/preview', thumbSrc: '/api/portal/images/2/thumb', alt: 'Ảnh Menu' },
]

describe('PortalLightbox (CF-06)', () => {
  it('fills the screen, shows the preview and has a large close button', () => {
    const onIndexChange = vi.fn()
    render(<PortalLightbox images={images} index={0} onIndexChange={onIndexChange} />)

    expect(screen.getByRole('dialog')).toHaveClass('h-[100dvh]')
    expect(screen.getByRole('img', { name: 'Ảnh Logo' })).toHaveAttribute('src', '/api/portal/images/1/preview')
    expect(screen.getByText('Logo · 08/11/2026 · 1 / 2')).toBeInTheDocument()
    const close = screen.getByRole('button', { name: 'Đóng' })
    expect(close).toHaveClass('h-12', 'w-12')
    fireEvent.click(close)
    expect(onIndexChange).toHaveBeenCalledWith(null)
  })

  it('zooms on double click and resets when paging', () => {
    const onIndexChange = vi.fn()
    const { rerender } = render(<PortalLightbox images={images} index={0} onIndexChange={onIndexChange} />)
    const img = screen.getByRole('img', { name: 'Ảnh Logo' })

    fireEvent.doubleClick(img, { clientX: 100, clientY: 100 })
    expect(img.style.transform).toContain('scale(2.5)')

    fireEvent.click(screen.getByRole('button', { name: 'Ảnh sau' }))
    expect(onIndexChange).toHaveBeenCalledWith(1)
    rerender(<PortalLightbox images={images} index={1} onIndexChange={onIndexChange} />)
    expect(screen.getByRole('img', { name: 'Ảnh Menu' }).style.transform).toContain('scale(1)')
  })

  it('blocks right-click and drag on the image', () => {
    render(<PortalLightbox images={images} index={0} onIndexChange={vi.fn()} />)
    const img = screen.getByRole('img', { name: 'Ảnh Logo' })
    expect(fireEvent.contextMenu(img)).toBe(false)
    expect(fireEvent.dragStart(img)).toBe(false)
  })
})
