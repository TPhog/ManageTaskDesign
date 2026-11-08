import type { StorageImage, StorageImageParams, StorageLevel, StorageUsage } from '@/lib/imageTypes'

/** Spec §2 thresholds, drawn as markers on the usage bar. */
export const STORAGE_MARKERS = [50, 70, 85, 90, 95] as const

/** "Xoá ảnh của dự án đã hoàn thành > 6 tháng" (spec §7). */
export const CLEANUP_SUGGESTION: StorageImageParams = { completedOnly: true, olderThanMonths: 6, sort: 'oldest' }

const SORTS = ['size', 'oldest', 'newest'] as const

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
