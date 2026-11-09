import { requestErrorMessage, requestSchema, toRequestForm } from '@/features/portal/requests'

describe('requestSchema with templates', () => {
  it('free text still needs content; a template makes it an optional note ≤ 2000', () => {
    expect(requestSchema.safeParse({ projectId: 'p1', deliverableId: '', content: '  ' }).success).toBe(false)
    expect(requestSchema.safeParse({ projectId: 'p1', deliverableId: '', templateId: 't1', content: '' }).success).toBe(true)
    const long = requestSchema.safeParse({ projectId: 'p1', deliverableId: '', templateId: 't1', content: 'a'.repeat(2001) })
    expect(long.success).toBe(false)
    expect(long.error?.issues[0].path).toEqual(['content'])
  })
})

describe('toRequestForm with a template', () => {
  it('adds templateId and the answers JSON, keeps the note as content', () => {
    const photo = new File(['x'], 'mau.png', { type: 'image/png' })
    const form = toRequestForm({ deliverableId: '', content: '  Gửi trước thứ 6 ', templateId: 't1' }, [photo],
      { headline: 'Khai trương', size: { preset: 'A3 (297 × 420 mm)' } })

    expect(form.get('templateId')).toBe('t1')
    expect(JSON.parse(form.get('answers') as string)).toEqual({ headline: 'Khai trương', size: { preset: 'A3 (297 × 420 mm)' } })
    expect(form.get('content')).toBe('Gửi trước thứ 6')
    expect(form.getAll('files')).toHaveLength(1)
  })

  it('sends no template fields for free text and no empty note', () => {
    const free = toRequestForm({ deliverableId: '', content: 'Đổi màu', templateId: '' })
    expect(free.has('templateId')).toBe(false)
    expect(free.has('answers')).toBe(false)
    expect(toRequestForm({ deliverableId: '', content: '   ', templateId: 't1' }, [], {}).has('content')).toBe(false)
  })

  it('explains a template the designer changed meanwhile', () => {
    expect(requestErrorMessage({ status: 400, errors: { templateId: ['x'] } })).toBe(
      'Mẫu yêu cầu vừa được nhà thiết kế thay đổi — vui lòng tải lại trang.')
  })
})
