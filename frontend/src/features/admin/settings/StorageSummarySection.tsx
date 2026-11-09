import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StorageUsageBar } from './StorageUsageBar'
import { useStorageUsage } from './useStorage'

/** Settings page card linking to Cài đặt › Dung lượng. */
export function StorageSummarySection() {
  const { data, isLoading, isError } = useStorageUsage()
  return (
    <section aria-labelledby="settings-storage" className="grid gap-6 rounded-xl border bg-card p-6 shadow-card md:grid-cols-3">
      <div>
        <h2 id="settings-storage" className="font-semibold text-foreground">Dung lượng ảnh</h2>
        <p className="mt-1 text-sm text-muted-foreground">Ảnh công việc, tiến độ và ảnh khách gửi. Tải ảnh mới bị chặn khi dùng ≥ 90%.</p>
      </div>
      <div className="space-y-4 md:col-span-2">
        {isLoading ? <Skeleton className="h-12 w-full" /> : isError || !data
          ? <p className="text-sm text-muted-foreground">Không tải được dung lượng.</p>
          : <StorageUsageBar usage={data} />}
        <Button asChild variant="outline"><Link to="/admin/settings/storage">Quản lý dung lượng</Link></Button>
      </div>
    </section>
  )
}
