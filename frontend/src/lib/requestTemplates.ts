import type { AnswerValue, SizeUnit, TemplateField, TemplateFieldType } from './requestTemplateTypes'

/** Mirrors TemplateLimits in backend Domain/RequestTemplates/TemplateField.cs. */
export const TEMPLATE_LIMITS = {
  maxFields: 30, maxLabel: 120, maxHelp: 300, maxOptions: 20, maxOption: 100,
  maxShortText: 200, maxLongText: 2000, maxNote: 2000, maxName: 100, maxDescription: 500,
  maxNumber: 1_000_000_000_000, maxSizeValue: 100_000,
} as const

export const SIZE_UNITS: SizeUnit[] = ['px', 'mm', 'cm', 'm']
export const FIELD_TYPES_WITH_OPTIONS: TemplateFieldType[] = ['SINGLE_CHOICE', 'MULTI_CHOICE', 'SIZE']
export const FIELD_TYPE_LABEL: Record<TemplateFieldType, string> = {
  SHORT_TEXT: 'Chữ ngắn', LONG_TEXT: 'Chữ dài', SINGLE_CHOICE: 'Chọn 1', MULTI_CHOICE: 'Chọn nhiều',
  SIZE: 'Kích thước', DATE: 'Ngày', NUMBER: 'Số', IMAGES: 'Ảnh tham khảo',
}
/** answerErrorsFromApi key for a form-level "answers" error */
export const FORM_ERROR_KEY = '_form'

export interface SizeDraft { mode: '' | 'preset' | 'custom'; preset: string; width: string; height: string; unit: SizeUnit }
export type AnswerDraft = string | string[] | SizeDraft
export type AnswerDrafts = Record<string, AnswerDraft>

export function emptyDraft(field: TemplateField): AnswerDraft {
  if (field.type === 'MULTI_CHOICE') return []
  if (field.type === 'SIZE') return { mode: '', preset: '', width: '', height: '', unit: 'cm' }
  return ''
}

export function emptyDrafts(fields: TemplateField[]): AnswerDrafts {
  return Object.fromEntries(fields.filter(f => f.type !== 'IMAGES').map(f => [f.key, emptyDraft(f)]))
}

const decimal = (s: string) => Number(s.trim().replace(',', '.'))
const integer = (s: string) => s.replace(/[.\s]/g, '')

/** JSON object sent as the multipart "answers" field; the server re-checks everything (BR-131). */
export function toAnswersPayload(fields: TemplateField[], drafts: AnswerDrafts): Record<string, AnswerValue> {
  const out: Record<string, AnswerValue> = {}
  for (const f of fields) {
    const d = drafts[f.key]
    if (d === undefined || f.type === 'IMAGES') continue
    if (f.type === 'MULTI_CHOICE') { if ((d as string[]).length > 0) out[f.key] = d as string[]; continue }
    if (f.type === 'SIZE') {
      const s = d as SizeDraft
      if (s.mode === 'preset' && s.preset) out[f.key] = { preset: s.preset }
      else if (s.mode === 'custom' && (s.width.trim() || s.height.trim())) out[f.key] = { width: decimal(s.width), height: decimal(s.height), unit: s.unit }
      continue
    }
    const text = (d as string).trim()
    if (!text) continue
    out[f.key] = f.type === 'NUMBER' ? Number(integer(text)) : text
  }
  return out
}

/** Same rules and Vietnamese messages as TemplateAnswers.Evaluate; keyed by field key. */
export function validateDrafts(fields: TemplateField[], drafts: AnswerDrafts, imageCount: number, todayIso: string): Record<string, string> {
  const errors: Record<string, string> = {}
  for (const f of fields) {
    if (f.type === 'IMAGES') { if (f.required && imageCount === 0) errors[f.key] = 'Đính kèm ít nhất 1 ảnh'; continue }
    const d = drafts[f.key] ?? emptyDraft(f)
    let empty = false
    if (f.type === 'MULTI_CHOICE') empty = (d as string[]).length === 0
    else if (f.type === 'SIZE') {
      const s = d as SizeDraft
      empty = !(s.mode === 'preset' && s.preset) && !(s.mode === 'custom' && (s.width.trim() || s.height.trim()))
      if (!empty && s.mode === 'custom') {
        const w = decimal(s.width), h = decimal(s.height)
        if (!(w > 0 && h > 0 && w <= TEMPLATE_LIMITS.maxSizeValue && h <= TEMPLATE_LIMITS.maxSizeValue)) {
          errors[f.key] = 'Nhập chiều rộng và chiều cao lớn hơn 0'; continue
        }
      }
    } else {
      const text = (d as string).trim()
      empty = text.length === 0
      if (!empty) {
        const max = f.type === 'LONG_TEXT' ? TEMPLATE_LIMITS.maxLongText : TEMPLATE_LIMITS.maxShortText
        if ((f.type === 'SHORT_TEXT' || f.type === 'LONG_TEXT') && text.length > max) { errors[f.key] = `Tối đa ${max} ký tự`; continue }
        if (f.type === 'DATE' && text < todayIso) { errors[f.key] = 'Chọn ngày từ hôm nay trở đi'; continue }
        if (f.type === 'NUMBER') {
          const raw = integer(text)
          if (!/^\d+$/.test(raw) || Number(raw) > TEMPLATE_LIMITS.maxNumber) { errors[f.key] = 'Nhập số nguyên từ 0 đến 1.000.000.000.000'; continue }
        }
      }
    }
    if (empty && f.required) errors[f.key] = 'Bắt buộc'
  }
  return errors
}

/** 400 ValidationProblem → { fieldKey: message, _form: message }. Other keys (contact, files…) are left to the caller. */
export function answerErrorsFromApi(errors: Record<string, string[]> | undefined): Record<string, string> {
  const out: Record<string, string> = {}
  for (const [key, messages] of Object.entries(errors ?? {})) {
    if (key === 'answers') out[FORM_ERROR_KEY] = messages[0]
    else if (key.startsWith('answers.')) out[key.slice('answers.'.length)] = messages[0]
  }
  return out
}

const KEY_PREFIX: Record<TemplateFieldType, string> = {
  SHORT_TEXT: 'text', LONG_TEXT: 'long', SINGLE_CHOICE: 'choice', MULTI_CHOICE: 'multi',
  SIZE: 'size', DATE: 'date', NUMBER: 'number', IMAGES: 'images',
}

export function newFieldKey(existing: string[], type: TemplateFieldType): string {
  for (let n = 1; ; n++) {
    const key = `${KEY_PREFIX[type]}_${n}`
    if (!existing.includes(key)) return key
  }
}

export function newField(type: TemplateFieldType, existingKeys: string[]): TemplateField {
  return {
    key: newFieldKey(existingKeys, type), label: 'Câu hỏi mới', type, required: false, help: null,
    options: type === 'SINGLE_CHOICE' || type === 'MULTI_CHOICE' ? ['Lựa chọn 1', 'Lựa chọn 2'] : [],
  }
}

export function moveItem<T>(items: T[], index: number, delta: -1 | 1): T[] {
  const target = index + delta
  if (index < 0 || index >= items.length || target < 0 || target >= items.length) return items
  const next = items.slice()
  ;[next[index], next[target]] = [next[target], next[index]]
  return next
}

/** One option per line; blanks dropped, duplicates (case-insensitive) dropped. */
export function parseOptions(text: string): string[] {
  const seen = new Set<string>()
  const out: string[] = []
  for (const line of text.split('\n').map(l => l.trim()).filter(Boolean)) {
    if (seen.has(line.toLowerCase())) continue
    seen.add(line.toLowerCase())
    out.push(line)
  }
  return out
}

/** Client copy of TemplateFieldRules.Normalize (first message, same wording) — the server stays the authority. */
export function fieldsError(fields: TemplateField[]): string | null {
  if (fields.length > TEMPLATE_LIMITS.maxFields) return `Tối đa ${TEMPLATE_LIMITS.maxFields} câu hỏi mỗi mẫu`
  const keys = new Set<string>()
  for (const [i, f] of fields.entries()) {
    const n = i + 1
    if (!/^[a-z0-9_]{1,40}$/.test(f.key)) return `Câu hỏi ${n}: mã chỉ gồm a-z, 0-9, _ (1–40 ký tự)`
    if (keys.has(f.key)) return `Câu hỏi ${n}: mã "${f.key}" bị trùng`
    keys.add(f.key)
    const label = f.label.trim()
    if (label.length < 1 || label.length > TEMPLATE_LIMITS.maxLabel) return `Câu hỏi ${n}: tiêu đề dài 1–${TEMPLATE_LIMITS.maxLabel} ký tự`
    if ((f.help ?? '').trim().length > TEMPLATE_LIMITS.maxHelp) return `Câu hỏi ${n}: gợi ý tối đa ${TEMPLATE_LIMITS.maxHelp} ký tự`
    if (FIELD_TYPES_WITH_OPTIONS.includes(f.type)) {
      const min = f.type === 'SIZE' ? 0 : 1
      if (f.options.length < min || f.options.length > TEMPLATE_LIMITS.maxOptions) return `Câu hỏi ${n}: cần ${min}–${TEMPLATE_LIMITS.maxOptions} lựa chọn`
      if (f.options.some(o => o.trim().length < 1 || o.trim().length > TEMPLATE_LIMITS.maxOption)) return `Câu hỏi ${n}: mỗi lựa chọn dài 1–${TEMPLATE_LIMITS.maxOption} ký tự`
    } else if (f.options.length > 0) return `Câu hỏi ${n}: loại này không có lựa chọn`
  }
  if (fields.filter(f => f.type === 'IMAGES').length > 1) return 'Mỗi mẫu chỉ có 1 câu hỏi ảnh đính kèm'
  return null
}

export const imagesField = (fields: TemplateField[]) => fields.find(f => f.type === 'IMAGES')
