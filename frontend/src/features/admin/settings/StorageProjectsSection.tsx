import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ImageOff } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { StatusBadge } from '@/components/StatusBadge'
import { formatBytes } from '@/lib/images'
import { completedLabel, projectUsageLine } from './storage'
import { useStorageProjects } from './useStorage'

const SHOWN_PROJECTS = 10

/** P10: usage per project, largest first; "Xem ảnh" filters the image list below. */
export function StorageProjectsSection({ onSelect }: { onSelect: (projectId: string) => void }) {
  const { data, isLoading, isError } = useStorageProjects()
  const [showAll, setShowAll] = useState(false)
  const shown = showAll ? data : data?.slice(0, SHOWN_PROJECTS)
  return (
    <section aria-label="Dung lượng theo dự án" className="rounded-xl border bg-card shadow-card">
      <h2 className="border-b px-5 py-4 font-display text-title font-bold text-foreground">Theo dự án</h2>
      {isLoading ? (
        <div className="space-y-2 p-5"><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /></div>
      ) : isError ? (
        <p className="p-5 text-sm text-muted-foreground">Không tải được dung lượng theo dự án.</p>
      ) : !data || data.length === 0 ? (
        <EmptyState icon={ImageOff} title="Chưa có ảnh nào" description="Ảnh công việc, tiến độ và ảnh khách gửi sẽ hiện ở đây." />
      ) : (
        <ul className="divide-y">
          {(shown ?? []).map(p => (
            <li key={p.projectId} className="flex flex-wrap items-center gap-3 px-5 py-3 transition-colors duration-150 hover:bg-surface-sunk">
              <div className="min-w-0 flex-1 space-y-0.5">
                <p className="truncate text-sm font-medium text-foreground">
                  <Link to={`/admin/projects/${p.projectId}`} className="link-hover">{p.projectName}</Link>
                </p>
                <p className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                  <span className="truncate">{p.clientName}</span>
                  {completedLabel(p.completedAt) ?? <StatusBadge status={p.projectStatus} />}
                </p>
                <p className="text-xs text-muted-foreground">{projectUsageLine(p)}</p>
              </div>
              <p className="text-sm font-medium tabular-nums text-foreground">{formatBytes(p.totalBytes)}</p>
              <Button type="button" variant="outline" size="sm" className="min-h-11"
                aria-label={`Xem ảnh của ${p.projectName}`} onClick={() => onSelect(p.projectId)}>
                Xem ảnh
              </Button>
            </li>
          ))}
        </ul>
      )}
      {data && !showAll && data.length > SHOWN_PROJECTS && (
        <div className="border-t px-5 py-3">
          <Button type="button" variant="outline" size="sm" className="min-h-11" onClick={() => setShowAll(true)}>
            {`Hiện thêm (${data.length - SHOWN_PROJECTS})`}
          </Button>
        </div>
      )}
    </section>
  )
}
