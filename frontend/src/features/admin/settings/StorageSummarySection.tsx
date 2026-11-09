import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { StorageUsageBar } from './StorageUsageBar'
import { useStorageUsage } from './useStorage'
import { SettingsSection } from './SettingsSection'

/** Settings page card linking to Cài đặt › Dung lượng. */
export function StorageSummarySection() {
  const { data, isLoading, isError } = useStorageUsage()
  return (
    <SettingsSection
      id="settings-storage"
      title="Dung lượng ảnh"
      description="Ảnh công việc, tiến độ và ảnh khách gửi. Tải ảnh mới bị chặn khi dùng ≥ 90%."
    >
      {isLoading ? <Skeleton className="h-12 w-full" /> : isError || !data
        ? <p className="text-sm text-muted-foreground">Không tải được dung lượng.</p>
        : <StorageUsageBar usage={data} />}
      <div className="flex justify-end border-t border-border pt-4">
        <Button asChild variant="outline"><Link to="/admin/settings/storage">Quản lý dung lượng</Link></Button>
      </div>
    </SettingsSection>
  )
}
