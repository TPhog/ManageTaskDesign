import { useState } from 'react'
import { Link } from 'react-router-dom'
import { MessagesSquare, Plus, Receipt } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { QuotaMeter } from '@/components/QuotaMeter'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/format'
import type { RevisionRound } from '@/lib/revisionTypes'
import type { ProjectDetail } from '@/lib/types'
import { AddRoundDialog } from '@/features/admin/revisions/AddRoundDialog'
import { AdditionalQuotationDialog } from '@/features/admin/revisions/AdditionalQuotationDialog'
import { RevisionItemRow } from '@/features/admin/revisions/RevisionItemRow'
import { allRounds, isQuotable, quotableItems } from '@/features/admin/revisions/revisionUtils'
import { useProjectRevisions } from '@/features/admin/revisions/useRevisions'

const panelClass = 'rounded-xl border bg-card shadow-card'

/** docs/08 …/revisions: deliverable → round → items; select CHARGEABLE items → "Tạo báo giá phát sinh" (REV-04). */
export function RevisionsTab({ project }: { project: ProjectDetail }) {
  const { data, isLoading, error, refetch } = useProjectRevisions(project.id)
  const [selected, setSelected] = useState<Set<string>>(() => new Set())
  const [addOpen, setAddOpen] = useState(false)
  const [quoteOpen, setQuoteOpen] = useState(false)

  if (isLoading) return <Skeleton className="h-64 w-full rounded-xl" />
  if (error || !data) {
    return (
      <div className={`${panelClass} flex items-center gap-2 p-6 text-sm text-muted-foreground`}>
        Không tải được vòng sửa.
        <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
      </div>
    )
  }

  const rounds = allRounds(data)
  const quotable = quotableItems(rounds)
  // only items that are still quotable after a refetch stay selected
  const chosen = quotable.filter(i => selected.has(i.id)).map(i => i.id)

  function toggle(id: string) {
    setSelected(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const roundList = (list: RevisionRound[]) => (
    <ol className="divide-y">
      {list.map(round => (
        <li key={round.id} className="px-4 py-3">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
            <span className="font-semibold text-foreground">Vòng {round.roundNo}</span>
            <span className="text-xs text-muted-foreground">nhận {formatDate(round.receivedAt)}</span>
            {round.sourceRequestId && (
              <Link to={`/admin/requests/${round.sourceRequestId}`} className="text-xs text-primary underline-offset-2 hover:underline">
                Từ yêu cầu
              </Link>
            )}
            {round.counted && <span className="text-xs text-emerald-700">Tính 1 vòng quota</span>}
          </div>
          <ul className="mt-2 space-y-2">
            {round.items.map(item => (
              <RevisionItemRow
                key={item.id}
                item={item}
                projectId={project.id}
                deliverableId={round.deliverableId}
                selectable={isQuotable(item)}
                selected={selected.has(item.id)}
                onToggle={() => toggle(item.id)}
              />
            ))}
          </ul>
        </li>
      ))}
    </ol>
  )

  return (
    <div className="space-y-4">
      <div className={`${panelClass} flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between`}>
        <p className="text-sm text-muted-foreground">{quotable.length} mục tính phí chưa báo giá</p>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setAddOpen(true)}>
            <Plus aria-hidden="true" />
            Thêm vòng sửa
          </Button>
          <Button size="sm" disabled={chosen.length === 0} onClick={() => setQuoteOpen(true)}>
            <Receipt aria-hidden="true" />
            Tạo báo giá phát sinh ({chosen.length})
          </Button>
        </div>
      </div>

      {rounds.length === 0 && (
        <div className={panelClass}>
          <EmptyState
            icon={MessagesSquare}
            title="Chưa có vòng sửa"
            description="Tiếp nhận yêu cầu ở mục Yêu cầu, hoặc thêm vòng sửa thủ công."
          />
        </div>
      )}

      {data.deliverables.map(d => (
        <section key={d.id} aria-label={d.name} className={panelClass}>
          <header className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex min-w-0 items-center gap-3">
              <h3 className="truncate text-sm font-semibold text-foreground">{d.name}</h3>
              <StatusBadge status={d.status} />
            </div>
            <div className="flex items-center gap-4">
              <div className="w-40">
                <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="vòng" />
              </div>
              <span className="text-xs text-muted-foreground">Giá vòng sửa thêm: <MoneyText amount={d.extraRoundPrice} /></span>
            </div>
          </header>
          {d.rounds.length === 0
            ? <p className="px-4 py-3 text-sm text-muted-foreground">Chưa có vòng sửa.</p>
            : roundList(d.rounds)}
        </section>
      ))}

      {data.projectRounds.length > 0 && (
        <section aria-label="Cấp dự án" className={panelClass}>
          <header className="border-b p-4">
            <h3 className="text-sm font-semibold text-foreground">Cấp dự án</h3>
            <p className="text-xs text-muted-foreground">Cấp dự án — không dùng vòng sửa miễn phí</p>
          </header>
          {roundList(data.projectRounds)}
        </section>
      )}

      <AddRoundDialog project={project} open={addOpen} onClose={() => setAddOpen(false)} />
      <AdditionalQuotationDialog
        projectId={project.id}
        revisionItemIds={chosen}
        open={quoteOpen}
        onClose={() => setQuoteOpen(false)}
        onCreated={() => setSelected(new Set())}
      />
    </div>
  )
}
