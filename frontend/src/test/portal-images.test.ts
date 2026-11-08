import { groupPortalImages, toLightbox } from '@/features/portal/images'
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
      { id: '9', src: '/api/portal/images/9/preview', thumbSrc: '/api/portal/images/9/thumb', alt: 'Ảnh Logo', caption: 'Logo' },
    ])
  })
})
