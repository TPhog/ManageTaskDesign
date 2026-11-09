import { Link } from 'react-router-dom'
import { HardDrive, TriangleAlert } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { formatPercent, usageBarClass } from '@/features/admin/settings/storage'
import { useStorageUsage } from '@/features/admin/settings/useStorage'
import { formatBytes } from '@/lib/images'
import type { StorageLevel, StorageUsage } from '@/lib/imageTypes'
import { cn } from '@/lib/utils'
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
    <div className="flex flex-1 flex-col gap-3">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="font-display text-[2.75rem] font-extrabold leading-none tracking-[-0.03em] tabular-nums text-foreground">{formatPercent(usage.percent)}</span>
        <span className="text-sm font-semibold tabular-nums text-muted-foreground">{used}</span>
      </div>
      <div
        role="progressbar"
        aria-label="Dung lượng ảnh đã dùng"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(100, Math.round(usage.percent))}
        aria-valuetext={`${formatPercent(usage.percent)}, ${used}`}
        className="h-3 overflow-hidden rounded-full bg-border"
      >
        <div className={`h-full rounded-full ${usageBarClass(usage.level)}`} style={{ width: `${Math.min(100, usage.percent)}%` }} />
      </div>
      <p className="text-sm font-medium text-foreground">{LEVEL_LABEL[usage.level]}</p>
      {usage.uploadsBlocked && (
        <p className="flex items-start gap-2 text-sm text-danger">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          Đang chặn tải ảnh mới — hãy xoá bớt ảnh cũ.
        </p>
      )}
      <Link to="/admin/settings/storage" className="mt-auto inline-flex min-h-11 items-center self-start text-sm link-inline">
        Quản lý dung lượng
      </Link>
    </div>
  )
}

export function StorageWidget({ className }: { className?: string }) {
  const { data, isLoading, error } = useStorageUsage()
  return (
    <Section title="Dung lượng ảnh" icon={HardDrive} className={cn('flex flex-col', className)}>
      {isLoading ? (
        <Skeleton className="h-20 w-full" />
      ) : error || !data ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được dung lượng.
        </p>
      ) : (
        <StorageWidgetView usage={data} />
      )}
    </Section>
  )
}
