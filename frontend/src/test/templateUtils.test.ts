import { MAX_TEMPLATE_STEPS, addStep, moveStep, templateError } from '@/features/admin/services/templateUtils'

describe('moveStep', () => {
  it('swaps with the neighbour and ignores moves past the ends', () => {
    expect(moveStep(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b'])
    expect(moveStep(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c'])
    const steps = ['a', 'b']
    expect(moveStep(steps, 0, -1)).toBe(steps)
    expect(moveStep(steps, 1, 1)).toBe(steps)
  })
})

describe('addStep', () => {
  it('appends a trimmed step', () => {
    expect(addStep(['a'], '  Phác thảo  ')).toEqual(['a', 'Phác thảo'])
  })

  it('ignores blank text and a full template', () => {
    const steps = ['a']
    expect(addStep(steps, '   ')).toBe(steps)
    const full = Array.from({ length: MAX_TEMPLATE_STEPS }, (_, i) => `Bước ${i + 1}`)
    expect(addStep(full, 'Thêm nữa')).toBe(full)
  })
})

describe('templateError', () => {
  it('accepts an empty or valid template', () => {
    expect(templateError([])).toBeNull()
    expect(templateError(['Moodboard', 'Phác thảo'])).toBeNull()
  })

  it('rejects blank, long or too many steps', () => {
    expect(templateError(['Moodboard', '  '])).toBe('Bước mẫu không được để trống')
    expect(templateError(['x'.repeat(201)])).toBe('Mỗi bước tối đa 200 ký tự')
    expect(templateError(Array.from({ length: 21 }, () => 'x'))).toBe('Tối đa 20 bước')
  })
})
