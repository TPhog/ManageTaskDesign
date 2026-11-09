import { Link } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowLeft, ArrowUp, ClipboardList, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { StatusBadge } from '@/components/StatusBadge'
import { designTypeLabel } from '@/features/portal/labels'
import { qk } from '@/lib/queryKeys'
import { moveItem } from '@/lib/requestTemplates'
import type { AdminRequestTemplate } from '@/lib/requestTemplateTypes'
import { useReorderRequestTemplates, useRequestTemplates } from './useRequestTemplates'

const MAX_TEMPLATES = 50

export default function RequestTemplatesPage() {
  const qc = useQueryClient()
  const list = useRequestTemplates()
  const reorder = useReorderRequestTemplates()
  const items = list.data ?? []

  function move(index: number, delta: -1 | 1) {
    // one full-list POST at a time: overlapping ones may land out of order
    if (reorder.isPending) return
    const next = moveItem(items, index, delta)
    if (next === items) return
    qc.setQueryData(qk.requestTemplates.all, next) // optimistic; onSettled refetches the truth
    reorder.mutate(next.map(t => t.id), { onError: () => toast.error('Không đổi được thứ tự. Vui lòng thử lại.') })
  }

  return (
    <div className="space-y-6">
      <Link to="/admin/settings?tab=portal" className="inline-flex min-h-10 items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" /> Cài đặt
      </Link>
      <PageHeader
        eyebrow="Cài đặt"
        title="Mẫu yêu cầu"
        description="Khách chọn mẫu khi gửi yêu cầu trên portal hoặc qua form khách mới."
        actions={
          items.length >= MAX_TEMPLATES ? (
            <Button disabled><Plus aria-hidden="true" /> Thêm mẫu</Button>
          ) : (
            <Button asChild><Link to="/admin/settings/request-templates/new"><Plus aria-hidden="true" /> Thêm mẫu</Link></Button>
          )
        }
      />

      {list.isLoading ? (
        <div className="space-y-3">{[0, 1, 2].map(i => <Skeleton key={i} className="h-20 w-full rounded-xl" />)}</div>
      ) : list.isError ? (
        <EmptyState icon={ClipboardList} title="Không tải được danh sách mẫu"
          action={<Button variant="outline" onClick={() => list.refetch()}>Thử lại</Button>} />
      ) : items.length === 0 ? (
        <EmptyState icon={ClipboardList} title="Chưa có mẫu nào" description="Tạo mẫu để khách trả lời đúng những gì bạn cần."
          action={<Button asChild><Link to="/admin/settings/request-templates/new">Thêm mẫu</Link></Button>} />
      ) : (
        <ul className="space-y-3">
          {items.map((t, i) => <TemplateRow key={t.id} t={t} index={i} count={items.length} saving={reorder.isPending} onMove={d => move(i, d)} />)}
        </ul>
      )}
    </div>
  )
}

function TemplateRow({ t, index, count, saving, onMove }: {
  t: AdminRequestTemplate; index: number; count: number; saving: boolean; onMove: (d: -1 | 1) => void
}) {
  return (
    <li className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-xl border bg-card p-4 shadow-card">
      <div className="min-w-0 flex-1 basis-60">
        <div className="flex flex-wrap items-center gap-2">
          <Link to={`/admin/settings/request-templates/${t.id}`} className="font-display text-base font-bold text-foreground link-hover">
            {t.name}
          </Link>
          <StatusBadge status={t.active ? 'ACTIVE' : 'INACTIVE'} label={t.active ? 'Đang dùng' : 'Đã ẩn'} />
        </div>
        {t.description && <p className="mt-0.5 line-clamp-1 text-sm text-muted-foreground">{t.description}</p>}
        <p className="mt-1 text-xs text-muted-foreground">
          {t.fields.length} câu hỏi · {t.serviceName ?? 'Chưa gắn dịch vụ'} · {designTypeLabel[t.designType] ?? t.designType}
        </p>
      </div>
      <div className="flex items-center gap-1">
        <Button type="button" variant="ghost" size="icon" className="h-9 w-9" aria-label={`Đưa mẫu ${t.name} lên`}
          disabled={saving || index === 0} onClick={() => onMove(-1)}>
          <ArrowUp aria-hidden="true" />
        </Button>
        <Button type="button" variant="ghost" size="icon" className="h-9 w-9" aria-label={`Đưa mẫu ${t.name} xuống`}
          disabled={saving || index === count - 1} onClick={() => onMove(1)}>
          <ArrowDown aria-hidden="true" />
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link to={`/admin/settings/request-templates/${t.id}`} aria-label={`Sửa mẫu ${t.name}`}>Sửa</Link>
        </Button>
      </div>
    </li>
  )
}
