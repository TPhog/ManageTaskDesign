import {
  FORM_ERROR_KEY, answerErrorsFromApi, emptyDrafts, fieldsError, moveItem, newField, newFieldKey, parseOptions,
  nothingAnswered, toAnswersPayload, validateDrafts, type AnswerDrafts,
} from '@/lib/requestTemplates'
import type { TemplateField } from '@/lib/requestTemplateTypes'

const fields: TemplateField[] = [
  { key: 'headline', label: 'Tiêu đề', type: 'SHORT_TEXT', required: true, help: null, options: [] },
  { key: 'info', label: 'Thông tin', type: 'MULTI_CHOICE', required: false, help: null, options: ['Giá', 'Địa chỉ'] },
  { key: 'size', label: 'Kích thước', type: 'SIZE', required: true, help: null, options: ['A3 (297 × 420 mm)'] },
  { key: 'deadline', label: 'Hạn', type: 'DATE', required: false, help: null, options: [] },
  { key: 'budget', label: 'Ngân sách', type: 'NUMBER', required: false, help: null, options: [] },
  { key: 'refs', label: 'Ảnh', type: 'IMAGES', required: true, help: null, options: [] },
]

describe('toAnswersPayload', () => {
  it('sends only answered questions, in the shape the server checks', () => {
    const drafts: AnswerDrafts = {
      ...emptyDrafts(fields),
      headline: '  Khai trương ',
      info: ['Giá'],
      size: { mode: 'custom', preset: '', width: '29,7', height: '42', unit: 'cm' },
      budget: '1.500.000',
    }
    expect(toAnswersPayload(fields, drafts)).toEqual({
      headline: 'Khai trương', info: ['Giá'], size: { width: 29.7, height: 42, unit: 'cm' }, budget: 1500000,
    })
  })

  it('sends a preset size as { preset } and never the images question', () => {
    const drafts: AnswerDrafts = { ...emptyDrafts(fields), size: { mode: 'preset', preset: 'A3 (297 × 420 mm)', width: '', height: '', unit: 'cm' } }
    expect(toAnswersPayload(fields, drafts)).toEqual({ size: { preset: 'A3 (297 × 420 mm)' } })
  })
})

describe('validateDrafts', () => {
  it('mirrors the server messages (BR-131)', () => {
    const drafts: AnswerDrafts = { ...emptyDrafts(fields), deadline: '2026-11-01', budget: 'abc' }
    expect(validateDrafts(fields, drafts, 0, '2026-11-13')).toEqual({
      headline: 'Bắt buộc',
      size: 'Bắt buộc',
      deadline: 'Chọn ngày từ hôm nay trở đi',
      budget: 'Nhập số nguyên từ 0 đến 1.000.000.000.000',
      refs: 'Đính kèm ít nhất 1 ảnh',
    })
  })

  it('accepts a complete answer set and checks custom sizes', () => {
    const ok: AnswerDrafts = {
      ...emptyDrafts(fields), headline: 'x', size: { mode: 'preset', preset: 'A3 (297 × 420 mm)', width: '', height: '', unit: 'cm' },
    }
    expect(validateDrafts(fields, ok, 1, '2026-11-13')).toEqual({})
    const zero: AnswerDrafts = { ...ok, size: { mode: 'custom', preset: '', width: '0', height: '10', unit: 'cm' } }
    expect(validateDrafts(fields, zero, 1, '2026-11-13')).toEqual({ size: 'Nhập chiều rộng và chiều cao lớn hơn 0' })
  })
})

describe('nothingAnswered', () => {
  it('is true only with no answer, no photo for the images question and no note', () => {
    const empty = emptyDrafts(fields)
    expect(nothingAnswered(fields, empty, 0, '  ')).toBe(true)
    expect(nothingAnswered(fields, empty, 0, 'Gấp')).toBe(false)
    expect(nothingAnswered(fields, empty, 1, '')).toBe(false)
    expect(nothingAnswered(fields.filter(f => f.type !== 'IMAGES'), empty, 1, '')).toBe(true)
    expect(nothingAnswered(fields, { ...empty, budget: '500.000' }, 0, '')).toBe(false)
  })
})

describe('answerErrorsFromApi', () => {
  it('maps answers.<key> to the question and answers to the form', () => {
    expect(answerErrorsFromApi({ 'answers.headline': ['Bắt buộc'], answers: ['Nội dung quá dài'], contactName: ['x'] })).toEqual({
      headline: 'Bắt buộc', [FORM_ERROR_KEY]: 'Nội dung quá dài',
    })
    expect(answerErrorsFromApi(undefined)).toEqual({})
  })
})

describe('template editing helpers', () => {
  it('creates unique lowercase keys and sensible defaults', () => {
    expect(newFieldKey(['size_1'], 'SIZE')).toBe('size_2')
    expect(newFieldKey([], 'SHORT_TEXT')).toBe('text_1')
    expect(newField('SINGLE_CHOICE', [])).toEqual({ key: 'choice_1', label: 'Câu hỏi mới', type: 'SINGLE_CHOICE', required: false, help: null, options: ['Lựa chọn 1', 'Lựa chọn 2'] })
    expect(newField('IMAGES', []).options).toEqual([])
  })

  it('moves, parses options and mirrors the server field rules (BR-130)', () => {
    expect(moveItem(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b'])
    expect(parseOptions(' Giá \n\nĐịa chỉ\nGiá ')).toEqual(['Giá', 'Địa chỉ'])
    expect(fieldsError([{ ...fields[1], options: [] }])).toBe('Câu hỏi 1: cần 1–20 lựa chọn')
    expect(fieldsError([fields[5], { ...fields[5], key: 'refs_2' }])).toBe('Mỗi mẫu chỉ có 1 câu hỏi ảnh đính kèm')
    expect(fieldsError([{ ...fields[0], label: ' ' }])).toBe('Câu hỏi 1: tiêu đề dài 1–120 ký tự')
    expect(fieldsError(fields)).toBeNull()
  })
})
