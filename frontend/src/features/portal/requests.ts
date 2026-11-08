import { z } from 'zod'
import type { ApiError } from '@/lib/api'
import { imageErrorMessage, isStorageFull } from '@/lib/images'
import type { ProjectStatus } from '@/lib/types'

export const REQUEST_MAX_LENGTH = 5000
/** BR-54 */
export const DAILY_REQUEST_LIMIT = 20

/** BR-21 / BR-53: the portal takes new requests only for these project statuses. */
export function acceptsRequests(status: ProjectStatus): boolean {
  return status === 'DRAFT' || status === 'QUOTED' || status === 'IN_PROGRESS' || status === 'ON_HOLD'
}

export const requestSchema = z.object({
  projectId: z.string().min(1, 'Chọn dự án'),
  /** '' = no specific deliverable */
  deliverableId: z.string(),
  content: z
    .string()
    .trim()
    .min(1, 'Nhập nội dung yêu cầu')
    .max(REQUEST_MAX_LENGTH, `Tối đa ${REQUEST_MAX_LENGTH} ký tự`),
})

export type RequestFormValues = z.infer<typeof requestSchema>

/** multipart body of POST /api/portal/projects/{id}/requests (docs/05 §4): text fields, then ≤ 5 photos (plan 08). */
export function toRequestForm(values: Pick<RequestFormValues, 'deliverableId' | 'content'>, files: File[] = []): FormData {
  const form = new FormData()
  form.append('content', values.content.trim())
  if (values.deliverableId) form.append('deliverableId', values.deliverableId)
  for (const f of files) form.append('files', f)
  return form
}

/** Vietnamese message for a failed request submission (docs/08 §4). */
export function requestErrorMessage(err: unknown): string {
  const e = (err ?? {}) as Partial<ApiError>
  if (e.status === 429) {
    return `Hôm nay bạn đã gửi ${DAILY_REQUEST_LIMIT} yêu cầu — vui lòng gửi tiếp vào ngày mai hoặc nhắn trực tiếp cho designer.`
  }
  if (isStorageFull(err) || e.status === 413 || e.status === 415) return imageErrorMessage(err, 'client')
  if (e.status === 401) return 'Phiên xem đã hết hạn — vui lòng mở lại link designer đã gửi.'
  if (e.status === 403) return 'Vui lòng tải lại trang rồi gửi lại.'
  if (e.status === 422) return unprocessableMessage(e.detail)
  if (e.status === 404) return 'Không tìm thấy dự án này.'
  const firstFieldError = e.errors ? Object.values(e.errors)[0]?.[0] : undefined
  return firstFieldError ?? 'Không gửi được yêu cầu. Vui lòng thử lại.'
}

const RULE_CODE = /\s*\(BR-\d+(?:\/\d+)*\)/g

/** 422 detail may carry internal rule codes (BR-xx): map known cases to a fixed message, never leak the code. */
function unprocessableMessage(detail?: string): string {
  if (!detail) return 'Dự án này không nhận yêu cầu mới.'
  if (/BR-21|BR-53/.test(detail)) return 'Dự án này đã hoàn thành hoặc đã huỷ nên không nhận yêu cầu mới.'
  if (/BR-54/.test(detail)) {
    return `Hôm nay bạn đã gửi ${DAILY_REQUEST_LIMIT} yêu cầu — vui lòng gửi tiếp vào ngày mai hoặc nhắn trực tiếp cho designer.`
  }
  return detail.replace(RULE_CODE, '').trim() || 'Dự án này không nhận yêu cầu mới.'
}
