import { Link, useParams } from 'react-router-dom'
import { ChevronRight, SearchX } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { QuotationEditor } from './QuotationEditor'
import { QuotationView } from './QuotationView'
import { useQuotation } from './useQuotations'

export default function QuotationPage() {
  const { id } = useParams<{ id: string }>()
  const { data: quotation, isLoading, error } = useQuotation(id)

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-5 w-48" />
        <Skeleton className="h-40 w-full rounded-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }

  if (error || !quotation) {
    return (
      <div className="rounded-xl border bg-card p-5 shadow-card">
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy báo giá"
          description="Báo giá có thể đã bị xoá hoặc đường dẫn không đúng."
          action={<Button asChild variant="outline"><Link to="/admin/projects">Về danh sách dự án</Link></Button>}
        />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/admin/projects" className="shrink-0 rounded-sm hover:text-foreground">Dự án</Link>
        <ChevronRight className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        <Link to={`/admin/projects/${quotation.projectId}`} className="truncate rounded-sm hover:text-foreground">
          {quotation.projectName}
        </Link>
        <ChevronRight className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        <span className="truncate font-medium text-foreground" aria-current="page">
          {quotation.number ? `${quotation.number} · v${quotation.version}` : 'Báo giá nháp'}
        </span>
      </nav>
      {/* key = id only: the editor adopts the server's answer after each save instead of remounting */}
      {quotation.status === 'DRAFT'
        ? <QuotationEditor key={quotation.id} quotation={quotation} />
        : <QuotationView quotation={quotation} />}
    </div>
  )
}
