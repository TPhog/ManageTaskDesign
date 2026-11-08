import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, ImageOff, Sparkles, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { EmptyState } from '@/components/EmptyState'
import { ImageLightbox } from '@/components/ImageLightbox'
import { PageHeader } from '@/components/PageHeader'
import { api } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { formatBytes, imageErrorMessage } from '@/lib/images'
import type { LightboxImage, StorageImageParams } from '@/lib/imageTypes'
import { qk } from '@/lib/queryKeys'
import type { PagedResult, ProjectListItem } from '@/lib/types'
import { OriginalCleanupSection } from './OriginalCleanupSection'
import { StorageProjectsSection } from './StorageProjectsSection'
import { StorageUsageBar } from './StorageUsageBar'
import { CLEANUP_SUGGESTION, clampPage, ownerLabel, readStorageParams, selectedBytes, toStorageQuery } from './storage'
import { useBulkDeleteImages, useStorageImages, useStorageUsage } from './useStorage'

const selectClass =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

/** Spec §7 "Dung lượng": usage with markers, filterable list, multi-select delete. Nothing is deleted automatically. */
export default function StoragePage() {
  const [search, setSearch] = useSearchParams()
  const params = readStorageParams(search)
  const usage = useStorageUsage()
  const list = useStorageImages(params)
  const bulk = useBulkDeleteImages()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [confirming, setConfirming] = useState(false)
  const [viewer, setViewer] = useState<number | null>(null)

  const { data: projects } = useQuery({
    queryKey: [...qk.projects.all, 'storage-filter'],
    queryFn: () => api.get<PagedResult<ProjectListItem>>('/admin/projects?pageSize=100'),
  })

  const items = useMemo(() => list.data?.items ?? [], [list.data])
  const lightbox: LightboxImage[] = useMemo(
    () => items.map(i => ({ id: i.id, src: i.previewUrl, thumbSrc: i.thumbUrl, alt: i.originalName, caption: i.projectName })),
    [items],
  )
  const pageCount = list.data ? Math.max(1, Math.ceil(list.data.total / list.data.pageSize)) : 1
  useEffect(() => {
    if (list.data && params.page > pageCount) {
      setSearch(new URLSearchParams(toStorageQuery({ ...params, page: clampPage(params.page, pageCount) })), { replace: true })
    }
  }, [list.data, params, pageCount, setSearch])
  const allOnPage = items.length > 0 && items.every(i => selected.has(i.id))

  function update(next: StorageImageParams) {
    setSelected(new Set())
    setSearch(new URLSearchParams(toStorageQuery({ ...params, page: 1, ...next })))
  }

  function toggle(id: string) {
    setSelected(current => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function togglePage() {
    setSelected(current => {
      const next = new Set(current)
      for (const i of items) {
        if (allOnPage) next.delete(i.id)
        else next.add(i.id)
      }
      return next
    })
  }

  function deleteSelected() {
    bulk.mutate([...selected], {
      onSuccess: r => {
        setConfirming(false)
        setSelected(new Set())
        toast.success(`Đã xoá ${r.deleted} ảnh, giải phóng ${formatBytes(r.freedBytes)}`
          + (r.skipped > 0 ? ` · bỏ qua ${r.skipped} ảnh (đã xoá hoặc là chứng từ báo giá)` : ''))
      },
      onError: err => toast.error(imageErrorMessage(err)),
    })
  }

  return (
    <div className="space-y-6">
      <Link to="/admin/settings" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Cài đặt
      </Link>
      <PageHeader title="Dung lượng" description="Ảnh đã lưu và hạn mức. Xoá ảnh để giải phóng dung lượng — không khôi phục được." />

      <section aria-label="Hạn mức" className="rounded-xl border bg-card p-5 shadow-card">
        {usage.isLoading ? <Skeleton className="h-14 w-full" /> : usage.data
          ? <StorageUsageBar usage={usage.data} />
          : <p className="text-sm text-muted-foreground">Không tải được dung lượng.</p>}
      </section>

      <StorageProjectsSection onSelect={projectId => update({ projectId })} />
      <OriginalCleanupSection />

      <section aria-label="Bộ lọc" className="space-y-4 rounded-xl border bg-card p-5 shadow-card">
        <div className="grid gap-4 sm:grid-cols-4">
          <div className="space-y-1.5 sm:col-span-2">
            <Label htmlFor="storage-project">Dự án</Label>
            <select id="storage-project" className={selectClass} value={params.projectId ?? ''}
              onChange={e => update({ projectId: e.target.value || undefined })}>
              <option value="">Tất cả dự án</option>
              {(projects?.items ?? []).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="storage-age">Tải lên</Label>
            <select id="storage-age" className={selectClass} value={params.olderThanMonths}
              onChange={e => update({ olderThanMonths: Number(e.target.value) })}>
              <option value={0}>Mọi lúc</option>
              <option value={3}>Cũ hơn 3 tháng</option>
              <option value={6}>Cũ hơn 6 tháng</option>
              <option value={12}>Cũ hơn 12 tháng</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="storage-sort">Sắp xếp</Label>
            <select id="storage-sort" className={selectClass} value={params.sort}
              onChange={e => update({ sort: e.target.value as StorageImageParams['sort'] })}>
              <option value="size">Dung lượng lớn nhất</option>
              <option value="oldest">Cũ nhất</option>
              <option value="newest">Mới nhất</option>
            </select>
          </div>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" className="h-4 w-4 accent-primary" checked={params.completedOnly}
              onChange={e => update({ completedOnly: e.target.checked })} />
            Chỉ dự án đã hoàn thành
          </label>
          <Button type="button" variant="outline" size="sm" onClick={() => update({ ...CLEANUP_SUGGESTION, projectId: undefined })}>
            <Sparkles aria-hidden="true" />
            Gợi ý: ảnh của dự án đã hoàn thành &gt; 6 tháng
          </Button>
        </div>
      </section>

      <section aria-label="Danh sách ảnh" className="rounded-xl border bg-card shadow-card">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b px-5 py-3">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-primary" checked={allOnPage} disabled={items.length === 0}
              onChange={togglePage} aria-label="Chọn tất cả ảnh trang này" />
            {list.data ? `${list.data.total} ảnh · ${formatBytes(list.data.totalBytes)}` : 'Đang tải…'}
          </label>
          <Button type="button" variant="destructive" size="sm" disabled={selected.size === 0} onClick={() => setConfirming(true)}>
            <Trash2 aria-hidden="true" />
            Xoá {selected.size} ảnh{selected.size > 0 ? ` (${formatBytes(selectedBytes(items, selected))})` : ''}
          </Button>
        </div>

        {list.isLoading ? (
          <div className="space-y-2 p-5"><Skeleton className="h-16 w-full" /><Skeleton className="h-16 w-full" /></div>
        ) : list.isError ? (
          <p className="p-5 text-sm text-muted-foreground">Không tải được danh sách ảnh.</p>
        ) : items.length === 0 ? (
          <EmptyState icon={ImageOff} title="Không có ảnh nào khớp bộ lọc" description="Đổi bộ lọc để xem ảnh khác." />
        ) : (
          <ul className="divide-y">
            {items.map((img, i) => (
              <li key={img.id} className="flex items-center gap-3 px-5 py-3">
                <input type="checkbox" className="h-4 w-4 accent-primary" checked={selected.has(img.id)}
                  onChange={() => toggle(img.id)} aria-label={`Chọn ${img.originalName}`} />
                <button type="button" onClick={() => setViewer(i)} aria-label={`Xem ảnh ${img.originalName}`}
                  className="h-14 w-14 shrink-0 overflow-hidden rounded-md border bg-muted">
                  <img src={img.thumbUrl} alt={img.originalName} loading="lazy" draggable={false} className="h-full w-full object-cover" />
                </button>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-foreground">{img.originalName}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    <Link to={`/admin/projects/${img.projectId}`} className="hover:underline">{img.projectName}</Link>
                    {` · ${img.clientName} · ${ownerLabel(img)}`}
                  </p>
                  {img.originalDeletedAt && (
                    <p className="text-xs text-amber-700">Đã xoá bản gốc · {formatDate(img.originalDeletedAt)}</p>
                  )}
                </div>
                <div className="text-right text-xs tabular-nums text-muted-foreground">
                  <p className="font-medium text-foreground">{formatBytes(img.sizeBytes)}</p>
                  <p>{formatDate(img.createdAt)}</p>
                </div>
              </li>
            ))}
          </ul>
        )}

        {pageCount > 1 && (
          <div className="flex items-center justify-between border-t px-5 py-3 text-sm">
            <Button type="button" variant="outline" size="sm" disabled={params.page <= 1}
              onClick={() => update({ page: params.page - 1 })}>Trang trước</Button>
            <span className="text-muted-foreground">Trang {params.page} / {pageCount}</span>
            <Button type="button" variant="outline" size="sm" disabled={params.page >= pageCount}
              onClick={() => update({ page: params.page + 1 })}>Trang sau</Button>
          </div>
        )}
      </section>

      <ImageLightbox images={lightbox} index={viewer} onIndexChange={setViewer} />
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={`Xoá ${selected.size} ảnh?`}
        description={`Giải phóng ${formatBytes(selectedBytes(items, selected))}. Ảnh và các bản xem trước bị xoá khỏi ổ lưu trữ, khách không xem được nữa. Không khôi phục được.`}
        confirmLabel="Xoá vĩnh viễn"
        destructive
        pending={bulk.isPending}
        onConfirm={deleteSelected}
      />
    </div>
  )
}
