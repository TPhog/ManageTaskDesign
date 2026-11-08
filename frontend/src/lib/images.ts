import type { ApiError } from './api'

export const IMAGE_ACCEPT = 'image/jpeg,image/png,image/webp'
export const MAX_IMAGE_MB = 20
export const MAX_ADMIN_IMAGES = 10
export const MAX_PORTAL_IMAGES = 5
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Client-side pre-check only — the API re-checks magic bytes and size. */
export function validateImageFiles(files: File[], max: number): { files: File[]; errors: string[] } {
  const errors: string[] = []
  const ok: File[] = []
  for (const f of files) {
    if (!ACCEPTED_TYPES.includes(f.type)) errors.push(`${f.name}: chỉ nhận ảnh JPEG, PNG hoặc WebP`)
    else if (f.size > MAX_IMAGE_MB * 1024 * 1024) errors.push(`${f.name}: ảnh vượt ${MAX_IMAGE_MB} MB`)
    else ok.push(f)
  }
  if (ok.length > max) {
    errors.push(`Tối đa ${max} ảnh mỗi lần`)
    return { files: ok.slice(0, max), errors }
  }
  return { files: ok, errors }
}

/** Vietnamese decimal comma: 1,5 MB · 10,00 GB. */
export function formatBytes(bytes: number): string {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(2).replace('.', ',')} GB`
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(1).replace('.', ',')} MB`
  return `${Math.round(bytes / 1024)} KB`
}

export function isStorageFull(err: unknown): boolean {
  return (err as Partial<ApiError> | null)?.code === 'STORAGE_FULL'
}

/** Message for a failed upload; the client never sees the designer's settings path. */
export function imageErrorMessage(err: unknown, audience: 'designer' | 'client' = 'designer'): string {
  const e = (err ?? {}) as Partial<ApiError>
  if (isStorageFull(err)) {
    return audience === 'designer'
      ? 'Dung lượng lưu trữ đã đạt 90% — xoá bớt ảnh trong Cài đặt › Dung lượng để tải ảnh mới.'
      : 'Hiện chưa nhận thêm ảnh — bạn gửi yêu cầu không kèm ảnh hoặc nhắn trực tiếp cho designer nhé.'
  }
  if (e.code === 'FILE_TOO_LARGE' || e.code === 'PAYLOAD_TOO_LARGE' || e.status === 413) {
    if (e.code === 'FILE_TOO_LARGE' && e.detail) return e.detail
    return `Ảnh vượt ${MAX_IMAGE_MB} MB — vui lòng chọn ảnh nhỏ hơn.`
  }
  if (e.code === 'UNSUPPORTED_FILE_TYPE' || e.status === 415) return 'Chỉ nhận ảnh JPEG, PNG hoặc WebP.'
  const field = e.errors ? Object.values(e.errors)[0]?.[0] : undefined
  return field ?? e.detail ?? 'Không tải được ảnh. Vui lòng thử lại.'
}

/** multipart body: text fields first, then one "files" part per image. */
export function toImageForm(files: File[], fields: Record<string, string> = {}): FormData {
  const form = new FormData()
  for (const [key, value] of Object.entries(fields)) form.append(key, value)
  for (const f of files) form.append('files', f)
  return form
}
