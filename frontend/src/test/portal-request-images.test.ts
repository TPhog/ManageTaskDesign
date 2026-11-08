import { requestErrorMessage, toRequestForm } from '@/features/portal/requests'

const png = (name: string) => new File([new Uint8Array(4)], name, { type: 'image/png' })

describe('toRequestForm with photos', () => {
  it('appends one "files" part per photo after the text fields', () => {
    const form = toRequestForm({ deliverableId: 'd1', content: ' Đổi màu ' }, [png('a.png'), png('b.png')])
    expect(form.get('content')).toBe('Đổi màu')
    expect(form.get('deliverableId')).toBe('d1')
    expect(form.getAll('files').map(f => (f as File).name)).toEqual(['a.png', 'b.png'])
  })

  it('stays text-only without photos', () => {
    expect(toRequestForm({ deliverableId: '', content: 'x' }).getAll('files')).toEqual([])
  })
})

describe('requestErrorMessage for photos', () => {
  it('never sends the client to the designer settings', () => {
    const msg = requestErrorMessage({ status: 507, code: 'STORAGE_FULL' })
    expect(msg).toContain('gửi yêu cầu không kèm ảnh')
    expect(msg).not.toContain('Cài đặt')
  })

  it('explains size and type refusals', () => {
    expect(requestErrorMessage({ status: 413, code: 'FILE_TOO_LARGE' })).toContain('20 MB')
    expect(requestErrorMessage({ status: 415, code: 'UNSUPPORTED_FILE_TYPE' })).toBe('Chỉ nhận ảnh JPEG, PNG hoặc WebP.')
  })
})
