import { z } from 'zod'
import type { ApiError } from '@/lib/api'
import { imageErrorMessage, isStorageFull } from '@/lib/images'
import type { AnswerValue } from '@/lib/requestTemplateTypes'

/** Mirrors IntakeSubmission.ValidateContact (BR-134). */
export const contactSchema = z.object({
  contactName: z.string().trim().min(1, 'Nhập tên của bạn').max(100, 'Tối đa 100 ký tự'),
  contactPhone: z.string().trim().refine(
    v => v.length <= 20 && /^[0-9+ .()-]+$/.test(v) && v.replace(/\D/g, '').length >= 8,
    'Nhập số điện thoại / Zalo hợp lệ'),
  contactEmail: z.string().trim().max(200, 'Tối đa 200 ký tự')
    .refine(v => v === '' || /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v), 'Email không hợp lệ'),
})
export type IntakeContact = z.input<typeof contactSchema>

export function toIntakeForm(args: {
  formToken: string; honeypot: string; contact: IntakeContact; templateId: string
  answers?: Record<string, AnswerValue>; note: string; files: File[]
}): FormData {
  const form = new FormData()
  form.append('formToken', args.formToken)
  form.append('website', args.honeypot)
  form.append('contactName', args.contact.contactName.trim())
  form.append('contactPhone', args.contact.contactPhone.trim())
  if (args.contact.contactEmail.trim()) form.append('contactEmail', args.contact.contactEmail.trim())
  if (args.templateId) {
    form.append('templateId', args.templateId)
    form.append('answers', JSON.stringify(args.answers ?? {}))
  }
  if (args.note.trim()) form.append('note', args.note.trim())
  for (const f of args.files) form.append('files', f)
  return form
}

export const isFormExpired = (err: unknown) => (err as Partial<ApiError> | null)?.code === 'FORM_EXPIRED'

/** Plain wording for someone who has never used the app (docs/08). */
export function intakeErrorMessage(err: unknown): string {
  const e = (err ?? {}) as Partial<ApiError>
  if (isFormExpired(err)) return 'Biểu mẫu đã hết hạn. Trang vừa được tải lại — bạn bấm Gửi thêm lần nữa nhé.'
  if (e.status === 429) {
    // RATE_LIMITED = a daily cap; its detail says which one (this sender's or the studio's) in plain words
    if (e.code === 'RATE_LIMITED') return e.detail?.trim() || 'Hôm nay studio đã nhận nhiều yêu cầu — bạn vui lòng liên hệ trực tiếp hoặc gửi lại vào ngày mai.'
    // bodiless 429 = the per-IP limiter of 5 sends per sliding hour
    return 'Bạn đã gửi nhiều lần — vui lòng thử lại sau ít phút (chậm nhất 1 giờ).'
  }
  if (isStorageFull(err)) return 'Hiện chưa nhận thêm ảnh được — bạn bỏ ảnh ra rồi gửi lại, hoặc gửi ảnh qua Zalo cho studio.'
  if (e.status === 413 || e.status === 415) return imageErrorMessage(err, 'client')
  if (e.status === 404) return 'Link này không còn hoạt động. Vui lòng liên hệ trực tiếp studio.'
  const first = e.errors ? Object.values(e.errors)[0]?.[0] : undefined
  return first ?? 'Chưa gửi được. Bạn kiểm tra kết nối mạng rồi thử lại nhé.'
}
