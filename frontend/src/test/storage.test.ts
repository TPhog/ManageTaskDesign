import {
  CLEANUP_MONTHS, CLEANUP_SUGGESTION, cleanupErrorMessage, cleanupSummary, completedLabel, projectUsageLine, clampPage, formatPercent, ownerLabel, readStorageParams, selectedBytes, storageBanner, toStorageQuery, usageBarClass,
} from '@/features/admin/settings/storage'
import { formatDate } from '@/lib/format'
import type { StorageImage, StorageProject, StorageUsage } from '@/lib/imageTypes'

const usage = (level: StorageUsage['level'], percent: number): StorageUsage =>
  ({ usedBytes: 0, quotaBytes: 100, percent, level, uploadsBlocked: percent >= 90, thresholds: [50, 70, 85, 90, 95] })

describe('storageBanner', () => {
  it('stays quiet below 85 %', () => {
    expect(storageBanner(usage('OK', 40))).toBeNull()
    expect(storageBanner(usage('NOTICE', 72.4))).toBeNull()
    expect(storageBanner(undefined)).toBeNull()
  })

  it('is yellow at 85 % and red once uploads are blocked', () => {
    expect(storageBanner(usage('WARNING', 86.2))).toEqual({ tone: 'warning', text: 'Dung lượng ảnh đã dùng 86,2% — nên dọn bớt ảnh cũ.' })
    expect(storageBanner(usage('BLOCKED', 91))?.tone).toBe('danger')
    expect(storageBanner(usage('BLOCKED', 91))?.text).toContain('đang chặn tải ảnh mới')
    expect(storageBanner(usage('CRITICAL', 96.5))?.text).toContain('96,5%')
  })
})

describe('helpers', () => {
  it('formats percent with a decimal comma and colours the bar by level', () => {
    expect(formatPercent(70)).toBe('70,0%')
    expect(usageBarClass('OK')).toBe('bg-emerald-500')
    expect(usageBarClass('WARNING')).toBe('bg-amber-500')
    expect(usageBarClass('CRITICAL')).toBe('bg-red-600')
  })

  it('labels the owner of an image', () => {
    expect(ownerLabel({ ownerType: 'TASK', taskTitle: 'Vẽ logo' })).toBe('Công việc: Vẽ logo')
    expect(ownerLabel({ ownerType: 'TASK', taskTitle: null })).toBe('Công việc đã xoá')
    expect(ownerLabel({ ownerType: 'PROGRESS_UPDATE', taskTitle: null })).toBe('Cập nhật tiến độ')
    expect(ownerLabel({ ownerType: 'CLIENT_REQUEST', taskTitle: null })).toBe('Ảnh khách gửi')
  })

  it('sums the selected sizes', () => {
    const items = [{ id: 'a', sizeBytes: 10 }, { id: 'b', sizeBytes: 5 }] as StorageImage[]
    expect(selectedBytes(items, new Set(['b']))).toBe(5)
  })

  it('round-trips filters through the query string, leaving defaults out', () => {
    expect(toStorageQuery({ sort: 'size', page: 1 })).toBe('')
    const q = toStorageQuery({ projectId: 'p1', ...CLEANUP_SUGGESTION, page: 2 })
    expect(q).toBe('?projectId=p1&completedOnly=1&olderThanMonths=6&sort=oldest&page=2')
    expect(readStorageParams(new URLSearchParams(q))).toEqual({
      projectId: 'p1', completedOnly: true, olderThanMonths: 6, sort: 'oldest', page: 2,
    })
    expect(readStorageParams(new URLSearchParams('?sort=bogus&page=-3'))).toEqual({
      projectId: undefined, completedOnly: false, olderThanMonths: 0, sort: 'size', page: 1,
    })
  })
})

describe('clampPage', () => {
  it('moves past-the-end pages back to the last valid page', () => {
    expect(clampPage(3, 2)).toBe(2)
    expect(clampPage(2, 2)).toBe(2)
    expect(clampPage(1, 1)).toBe(1)
    expect(clampPage(4, 0)).toBe(1)
  })
})

describe('original cleanup helpers', () => {
  it('summarises the preview or says there is nothing to clean', () => {
    expect(cleanupSummary({ imageCount: 0, projectCount: 0, freedBytes: 0 })).toBe('Không có bản gốc nào để xoá.')
    expect(cleanupSummary({ imageCount: 30, projectCount: 2, freedBytes: 40 * 1024 ** 2 }))
      .toBe('Có thể giải phóng 40,0 MB: 30 bản gốc ở 2 dự án.')
    expect(CLEANUP_MONTHS).toEqual([3, 6, 12, 24])
  })

  it('labels completion and per-project usage', () => {
    expect(completedLabel(null)).toBeNull()
    expect(completedLabel('2026-03-02T09:00:00Z')).toBe(`Hoàn thành ${formatDate('2026-03-02T09:00:00Z')}`)
    const p: StorageProject = {
      projectId: 'p', projectName: 'Logo', clientName: 'Mây', projectStatus: 'COMPLETED', completedAt: null,
      imageCount: 24, totalBytes: 0, originalBytes: 30 * 1024 ** 2, originalsDeleted: 0,
    }
    expect(projectUsageLine(p)).toBe('24 ảnh · bản gốc 30,0 MB')
    expect(projectUsageLine({ ...p, originalsDeleted: 5 })).toBe('24 ảnh · bản gốc 30,0 MB · đã xoá 5 bản gốc')
  })

  it('explains a stale preview', () => {
    expect(cleanupErrorMessage({ status: 409, code: 'CONCURRENCY_CONFLICT' })).toBe('Danh sách đã thay đổi, xem lại')
    expect(cleanupErrorMessage({ status: 500 })).toBe('Không xoá được bản gốc. Vui lòng thử lại.')
  })
})
