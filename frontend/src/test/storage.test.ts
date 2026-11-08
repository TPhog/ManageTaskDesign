import {
  CLEANUP_SUGGESTION, formatPercent, ownerLabel, readStorageParams, selectedBytes, storageBanner, toStorageQuery, usageBarClass,
} from '@/features/admin/settings/storage'
import type { StorageImage, StorageUsage } from '@/lib/imageTypes'

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
