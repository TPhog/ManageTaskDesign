import { groupPortalImages, imagesOfDeliverable, toLightbox } from '@/features/portal/images'
import { formatDate } from '@/lib/format'
import type { PortalImage } from '@/lib/imageTypes'

const img = (id: string, source: PortalImage['source'], deliverableName: string | null = null): PortalImage => ({
  id, source, deliverableId: deliverableName ? `d-${deliverableName}` : null, deliverableName,
  width: 100, height: 80, createdAt: '2026-11-08T03:00:00Z',
  thumbUrl: `/api/portal/images/${id}/thumb`, previewUrl: `/api/portal/images/${id}/preview`,
})

describe('groupPortalImages', () => {
  it('groups by deliverable in first-seen order, then progress photos, then the rest', () => {
    const groups = groupPortalImages([
      img('1', 'PROGRESS_UPDATE'),
      img('2', 'TASK', 'Logo chính'),
      img('3', 'TASK', 'Bộ nhận diện'),
      img('4', 'TASK', 'Logo chính'),
      img('5', 'TASK'),
    ])
    expect(groups.map(g => [g.title, g.images.map(i => i.id)])).toEqual([
      ['Logo chính', ['2', '4']],
      ['Bộ nhận diện', ['3']],
      ['Cập nhật tiến độ', ['1']],
      ['Ảnh khác', ['5']],
    ])
  })
})

describe('toLightbox', () => {
  it('uses the watermarked preview in the viewer and the thumb in the grid', () => {
    expect(toLightbox([img('9', 'TASK', 'Logo')])).toEqual([
      { id: '9', src: '/api/portal/images/9/preview', thumbSrc: '/api/portal/images/9/thumb', alt: 'Ảnh Logo', caption: `Logo · ${formatDate('2026-11-08T03:00:00Z')}` },
    ])
  })
})

describe('imagesOfDeliverable', () => {
  it('keeps only the images of one product for its card (CF-07)', () => {
    const imgs = [img('a', 'TASK', 'Logo'), img('b', 'TASK', 'Menu'), img('c', 'PROGRESS_UPDATE')]
    expect(imagesOfDeliverable(imgs, 'd-Logo').map(i => i.id)).toEqual(['a'])
  })

  it('captions progress photos with their date', () => {
    expect(toLightbox([img('p', 'PROGRESS_UPDATE')])[0].caption).toBe(`Cập nhật tiến độ · ${formatDate('2026-11-08T03:00:00Z')}`)
  })
})
