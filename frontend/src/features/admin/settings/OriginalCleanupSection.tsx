import { useState } from 'react'
import type { OriginalCleanupPreview } from '@/lib/imageTypes'
import { Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { formatBytes } from '@/lib/images'
import { CLEANUP_MONTHS, DEFAULT_CLEANUP_MONTHS, cleanupErrorMessage, cleanupSummary, completedLabel } from './storage'
import { OriginalCleanupDialog } from './OriginalCleanupDialog'
import { useOriginalCleanup, useOriginalCleanupPreview } from './useStorage'

const SHOWN_PROJECTS = 5

/** P10 (BR-89): "Xoá bản gốc của dự án đã hoàn thành cũ hơn N tháng, giữ bản xem/thumbnail" — preview, confirm, never automatic. */
export function OriginalCleanupSection() {
  const [months, setMonths] = useState<number>(DEFAULT_CLEANUP_MONTHS)
  // snapshot of the preview the designer is confirming: numbers under the ticked checkbox cannot change
  const [confirming, setConfirming] = useState<OriginalCleanupPreview | null>(null)
  const preview = useOriginalCleanupPreview(months)
  const cleanup = useOriginalCleanup()
  const data = preview.data

  function run() {
    if (!confirming) return
    cleanup.mutate(
      {
        olderThanMonths: confirming.olderThanMonths,
        expectedImageCount: confirming.imageCount,
        expectedFreedBytes: confirming.freedBytes,
        expectedDigest: confirming.digest,
      },
      {
        onSuccess: r => {
          setConfirming(null)
          toast.success(`Đã xoá ${r.imageCount} bản gốc, giải phóng ${formatBytes(r.freedBytes)}`)
        },
        onError: err => {
          setConfirming(null)
          toast.error(cleanupErrorMessage(err))
        },
      },
    )
  }

  return (
    <section aria-label="Giải phóng bản gốc" className="space-y-4 rounded-xl border bg-card p-5 shadow-card">
      <div>
        <h2 className="font-semibold text-foreground">Xoá bản gốc của dự án đã hoàn thành</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Giữ bản xem có watermark và ảnh thu nhỏ — khách vẫn xem như cũ. Chỉ chạy khi bạn bấm, không bao giờ tự động.
        </p>
      </div>
      <div role="group" aria-label="Hoàn thành cách đây hơn" className="flex flex-wrap gap-2">
        {CLEANUP_MONTHS.map(m => (
          <Button key={m} type="button" size="sm" className="min-h-11" variant={m === months ? 'default' : 'outline'}
            aria-pressed={m === months} onClick={() => setMonths(m)}>
            {m} tháng
          </Button>
        ))}
      </div>
      {preview.isLoading ? (
        <Skeleton className="h-10 w-full" />
      ) : preview.isError || !data ? (
        <p className="text-sm text-muted-foreground">Không tải được số liệu bản gốc.</p>
      ) : (
        <div className="space-y-3">
          <p className="text-sm font-medium text-foreground" aria-live="polite">{cleanupSummary(data)}</p>
          {data.projects.length > 0 && (
            <ul aria-label="Dự án sẽ được dọn" className="divide-y rounded-md border text-sm">
              {data.projects.slice(0, SHOWN_PROJECTS).map(p => (
                <li key={p.projectId} className="flex items-center justify-between gap-3 px-3 py-2">
                  <span className="min-w-0 truncate text-foreground">
                    {p.projectName} <span className="text-muted-foreground">· {completedLabel(p.completedAt)}</span>
                  </span>
                  <span className="shrink-0 tabular-nums text-muted-foreground">{p.imageCount} ảnh · {formatBytes(p.freedBytes)}</span>
                </li>
              ))}
              {data.projects.length > SHOWN_PROJECTS && (
                <li className="px-3 py-2 text-muted-foreground">và {data.projects.length - SHOWN_PROJECTS} dự án khác</li>
              )}
            </ul>
          )}
          <Button type="button" variant="destructive" className="min-h-11" disabled={data.imageCount === 0 || preview.isFetching || cleanup.isPending}
            onClick={() => setConfirming(data)}>
            <Trash2 aria-hidden="true" />
            Xoá bản gốc…
          </Button>
        </div>
      )}
      {confirming && (
        <OriginalCleanupDialog open onOpenChange={() => setConfirming(null)} preview={confirming} pending={cleanup.isPending} onConfirm={run} />
      )}
    </section>
  )
}
