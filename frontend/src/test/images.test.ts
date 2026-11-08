import { formatBytes, imageErrorMessage, isStorageFull, toImageForm, validateImageFiles } from '@/lib/images'

const file = (name: string, type: string, size = 10) => new File([new Uint8Array(size)], name, { type })

describe('validateImageFiles', () => {
  it('keeps jpeg/png/webp and explains what was dropped', () => {
    const { files, errors } = validateImageFiles(
      [file('a.png', 'image/png'), file('b.gif', 'image/gif'), file('c.webp', 'image/webp')], 5)
    expect(files.map(f => f.name)).toEqual(['a.png', 'c.webp'])
    expect(errors).toEqual(['b.gif: chỉ nhận ảnh JPEG, PNG hoặc WebP'])
  })

  it('drops files over 20 MB and caps the count', () => {
    const big = file('big.jpg', 'image/jpeg', 20 * 1024 * 1024 + 1)
    const { files, errors } = validateImageFiles([big, ...Array.from({ length: 6 }, (_, i) => file(`${i}.png`, 'image/png'))], 5)
    expect(files).toHaveLength(5)
    expect(errors).toEqual(['big.jpg: ảnh vượt 20 MB', 'Tối đa 5 ảnh mỗi lần'])
  })
})

describe('formatBytes', () => {
  it.each([
    [0, '0 KB'],
    [2048, '2 KB'],
    [1_572_864, '1,5 MB'],
    [10_737_418_240, '10,00 GB'],
  ])('%d → %s', (bytes, text) => expect(formatBytes(bytes)).toBe(text))
})

describe('imageErrorMessage', () => {
  it('points the designer to the storage page when full', () => {
    const err = { status: 507, code: 'STORAGE_FULL' }
    expect(isStorageFull(err)).toBe(true)
    expect(imageErrorMessage(err)).toContain('Cài đặt › Dung lượng')
    expect(imageErrorMessage(err, 'client')).not.toContain('Cài đặt')
  })

  it('maps size and type errors, then field errors', () => {
    expect(imageErrorMessage({ status: 413, code: 'FILE_TOO_LARGE' })).toBe('Ảnh vượt 20 MB — vui lòng chọn ảnh nhỏ hơn.')
    expect(imageErrorMessage({ status: 413, code: 'FILE_TOO_LARGE', detail: 'Ảnh vượt 50 megapixel.' })).toBe('Ảnh vượt 50 megapixel.')
    expect(imageErrorMessage({ status: 415, code: 'UNSUPPORTED_FILE_TYPE' })).toBe('Chỉ nhận ảnh JPEG, PNG hoặc WebP.')
    expect(imageErrorMessage({ status: 400, errors: { files: ['Tối đa 10 ảnh mỗi lần gửi'] } })).toBe('Tối đa 10 ảnh mỗi lần gửi')
    expect(imageErrorMessage(new Error('x'))).toBe('Không tải được ảnh. Vui lòng thử lại.')
  })
})

describe('toImageForm', () => {
  it('adds fields then one "files" part per image', () => {
    const form = toImageForm([file('a.png', 'image/png'), file('b.png', 'image/png')], { message: 'Đã xong' })
    expect(form.get('message')).toBe('Đã xong')
    expect(form.getAll('files').map(f => (f as File).name)).toEqual(['a.png', 'b.png'])
  })
})
