import { Button } from '@/components/ui/button'

interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  /** noun after the total, e.g. "khách", "dự án" */
  itemLabel: string
}

/** QA D-06: "1–20 / 134 khách" + previous/next; hidden when everything fits on one page. */
export function Pagination({ page, pageSize, total, onPageChange, itemLabel }: PaginationProps) {
  if (total <= pageSize) return null
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const from = (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <nav aria-label="Phân trang" className="flex flex-wrap items-center justify-between gap-3 border-t p-4 text-sm text-muted-foreground">
      <span className="tabular-nums">{from}–{to} / {total} {itemLabel}</span>
      <div className="flex items-center gap-2">
        <span className="hidden tabular-nums sm:inline">Trang {page}/{pages}</span>
        <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Trang trước</Button>
        <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => onPageChange(page + 1)}>Trang sau</Button>
      </div>
    </nav>
  )
}
