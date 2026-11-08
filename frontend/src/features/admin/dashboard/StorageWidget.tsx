import { Link } from 'react-router-dom'
import { HardDrive, TriangleAlert } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { formatPercent, usageBarClass } from '@/features/admin/settings/storage'
import { useStorageUsage } from '@/features/admin/settings/useStorage'
import { formatBytes } from '@/lib/images'
import type { StorageLevel, StorageUsage } from '@/lib/imageTypes'
import { Section } from './DashboardSection'

const LEVEL_LABEL: Record<StorageLevel, string> = {
  OK: 'Còn thoải mái',
  NOTICE: 'Đã dùng khá nhiều',
  WARNING: 'Gần đầy',
  BLOCKED: 'Đã đầy',
  CRITICAL: 'Sắp hết chỗ',
}

/** Presentational: image storage usage as a single bar with the level spelled out. */
export function StorageWidgetView({ usage }: { usage: StorageUsage }) {
  const used = `${formatBytes(usage.usedBytes)} / ${formatBytes(usage.quotaBytes)}`
  return (
    <div className="space-y-3">
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-2xl font-semibold tabular-nums text-foreground">{formatPercent(usage.percent)}</span>
        <span className="text-sm text-muted-foreground">{LEVEL_LABEL[usage.level]}</span>
      </div>
      <div
        role="progressbar"
        aria-label="Dung lượng ảnh đã dùng"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(100, Math.round(usage.percent))}
        aria-valuetext={`${formatPercent(usage.percent)}, ${used}`}
        className="h-2.5 overflow-hidden rounded-full bg-muted"
      >
        <div className={`h-full ${usageBarClass(usage.level)}`} style={{ width: `${Math.min(100, usage.percent)}%` }} />
      </div>
      <p className="text-xs tabular-nums text-muted-foreground">{used}</p>
      {usage.uploadsBlocked && (
        <p className="flex items-start gap-2 text-sm text-red-700">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          Đang chặn tải ảnh mới — hãy xoá bớt ảnh cũ.
        </p>
      )}
      <Link to="/admin/settings/storage" className="inline-flex min-h-11 items-center text-sm text-primary underline-offset-2 hover:underline">
        Quản lý dung lượng
      </Link>
    </div>
  )
}

export function StorageWidget() {
  const { data, isLoading, error } = useStorageUsage()
  return (
    <Section title="Dung lượng ảnh" icon={HardDrive}>
      {isLoading ? (
        <Skeleton className="h-20 w-full" />
      ) : error || !data ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được dung lượng.
        </p>
      ) : (
        <StorageWidgetView usage={data} />
      )}
    </Section>
  )
}
