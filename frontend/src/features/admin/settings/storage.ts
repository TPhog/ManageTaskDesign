import { formatDate } from '@/lib/format'
import { formatBytes, imageErrorMessage } from '@/lib/images'
import type { OriginalCleanupPreview, StorageImage, StorageImageParams, StorageLevel, StorageProject, StorageUsage } from '@/lib/imageTypes'

/** Spec §2 thresholds, drawn as markers on the usage bar. */
export const STORAGE_MARKERS = [50, 70, 85, 90, 95] as const

/** "Xoá ảnh của dự án đã hoàn thành > 6 tháng" (spec §7). */
export const CLEANUP_SUGGESTION: StorageImageParams = { completedOnly: true, olderThanMonths: 6, sort: 'oldest' }

const SORTS = ['size', 'oldest', 'newest'] as const

/** Last valid page when the current one fell past the end (e.g. after bulk-deleting the last page). */
export function clampPage(page: number, pageCount: number): number {
  return Math.min(Math.max(1, page), Math.max(1, pageCount))
}

export function formatPercent(percent: number): string {
  return `${percent.toFixed(1).replace('.', ',')}%`
}

/** Yellow from 85 %, red once uploads are blocked (90 %) — spec §2. */
export function storageBanner(usage: StorageUsage | undefined): { tone: 'warning' | 'danger'; text: string } | null {
  if (!usage) return null
  const p = formatPercent(usage.percent)
  switch (usage.level) {
    case 'WARNING': return { tone: 'warning', text: `Dung lượng ảnh đã dùng ${p} — nên dọn bớt ảnh cũ.` }
    case 'BLOCKED': return { tone: 'danger', text: `Dung lượng ảnh đã dùng ${p} — đang chặn tải ảnh mới.` }
    case 'CRITICAL': return { tone: 'danger', text: `Dung lượng ảnh đã dùng ${p} — sắp đầy, hãy xoá bớt ảnh ngay.` }
    default: return null
  }
}

export function usageBarClass(level: StorageLevel): string {
  switch (level) {
    case 'OK': return 'bg-emerald-500'
    case 'NOTICE': return 'bg-sky-500'
    case 'WARNING': return 'bg-amber-500'
    case 'BLOCKED': return 'bg-red-500'
    case 'CRITICAL': return 'bg-red-600'
  }
}

export function ownerLabel(img: Pick<StorageImage, 'ownerType' | 'taskTitle'>): string {
  switch (img.ownerType) {
    case 'TASK': return img.taskTitle ? `Công việc: ${img.taskTitle}` : 'Công việc đã xoá'
    case 'PROGRESS_UPDATE': return 'Cập nhật tiến độ'
    case 'CLIENT_REQUEST': return 'Ảnh khách gửi'
    case 'DELIVERABLE': return 'Bàn giao'
  }
}

export function selectedBytes(items: StorageImage[], selected: ReadonlySet<string>): number {
  return items.filter(i => selected.has(i.id)).reduce((sum, i) => sum + i.sizeBytes, 0)
}

/** Query string for the page URL and the API; defaults (sort=size, page=1, no filter) are left out. */
export function toStorageQuery(params: StorageImageParams): string {
  const q = new URLSearchParams()
  if (params.projectId) q.set('projectId', params.projectId)
  if (params.completedOnly) q.set('completedOnly', '1')
  if (params.olderThanMonths) q.set('olderThanMonths', String(params.olderThanMonths))
  if (params.sort && params.sort !== 'size') q.set('sort', params.sort)
  if (params.page && params.page > 1) q.set('page', String(params.page))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export function readStorageParams(search: URLSearchParams): Required<Omit<StorageImageParams, 'pageSize' | 'projectId'>> & { projectId?: string } {
  const sort = search.get('sort')
  const months = Number(search.get('olderThanMonths') ?? 0)
  const page = Number(search.get('page') ?? 1)
  return {
    projectId: search.get('projectId') ?? undefined,
    completedOnly: search.get('completedOnly') === '1',
    olderThanMonths: Number.isInteger(months) && months > 0 && months <= 120 ? months : 0,
    sort: SORTS.includes(sort as (typeof SORTS)[number]) ? (sort as (typeof SORTS)[number]) : 'size',
    page: Number.isInteger(page) && page > 1 ? page : 1,
  }
}

/** P10 "Xoá bản gốc của dự án đã hoàn thành cũ hơn N tháng" — choices on the page (API accepts 1–120). */
export const CLEANUP_MONTHS = [3, 6, 12, 24] as const
export const DEFAULT_CLEANUP_MONTHS = 6

export function cleanupSummary(p: Pick<OriginalCleanupPreview, 'imageCount' | 'projectCount' | 'freedBytes'>): string {
  if (p.imageCount === 0) return 'Không có bản gốc nào để xoá.'
  return `Có thể giải phóng ${formatBytes(p.freedBytes)}: ${p.imageCount} bản gốc ở ${p.projectCount} dự án.`
}

export function completedLabel(completedAt: string | null): string | null {
  return completedAt ? `Hoàn thành ${formatDate(completedAt)}` : null
}

export function projectUsageLine(p: Pick<StorageProject, 'imageCount' | 'originalBytes' | 'originalsDeleted'>): string {
  const parts = [`${p.imageCount} ảnh`, `bản gốc ${formatBytes(p.originalBytes)}`]
  if (p.originalsDeleted > 0) parts.push(`đã xoá ${p.originalsDeleted} bản gốc`)
  return parts.join(' · ')
}

/** 409 = the candidate set changed since the preview; the page refreshes the preview and asks for a re-check. */
export function isCleanupStale(err: unknown): boolean {
  return (err as { code?: string } | null)?.code === 'CONCURRENCY_CONFLICT'
}

export function cleanupErrorMessage(err: unknown): string {
  if (isCleanupStale(err)) return 'Danh sách đã thay đổi, xem lại'
  const e = (err ?? {}) as { status?: number }
  return e.status === 400 || e.status === 409 ? imageErrorMessage(err) : 'Không xoá được bản gốc. Vui lòng thử lại.'
}
