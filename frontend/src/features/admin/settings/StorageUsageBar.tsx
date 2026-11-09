import type { StorageUsage } from '@/lib/imageTypes'
import { formatBytes } from '@/lib/images'
import { cn } from '@/lib/utils'
import { STORAGE_MARKERS, formatPercent, usageBarClass } from './storage'

/** Used / quota with the 50/70/85/90/95 % markers (spec §7). */
export function StorageUsageBar({ usage }: { usage: StorageUsage }) {
  const width = Math.min(100, Math.max(0, usage.percent))
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-foreground">
          {formatBytes(usage.usedBytes)} / {formatBytes(usage.quotaBytes)}
        </span>
        <span className="tabular-nums text-muted-foreground">{formatPercent(usage.percent)}</span>
      </div>
      <div
        role="progressbar"
        aria-label="Dung lượng đã dùng"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={usage.percent}
        aria-valuetext={`${formatPercent(usage.percent)} — ${formatBytes(usage.usedBytes)} trên ${formatBytes(usage.quotaBytes)}`}
        className="relative h-3 overflow-hidden rounded-full bg-muted"
      >
        <div className={cn('h-full rounded-full transition-[width]', usageBarClass(usage.level))} style={{ width: `${width}%` }} />
        {STORAGE_MARKERS.map(m => (
          <span key={m} className="absolute inset-y-0 w-px bg-foreground/30" style={{ left: `${m}%` }} aria-hidden="true" />
        ))}
      </div>
      <div className="relative h-4 text-[10px] text-muted-foreground" aria-hidden="true">
        {STORAGE_MARKERS.map(m => (
          <span key={m} className="absolute -translate-x-1/2" style={{ left: `${m}%` }}>{m}%</span>
        ))}
      </div>
      {usage.uploadsBlocked && (
        <p className="text-sm font-medium text-danger">Đang chặn tải ảnh mới (≥ 90%). Xoá bớt ảnh để mở lại.</p>
      )}
    </div>
  )
}
