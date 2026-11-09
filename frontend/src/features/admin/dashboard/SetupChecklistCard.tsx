import { Link } from 'react-router-dom'
import { CheckCircle2, Landmark, ListChecks, Stamp, Store, Tags, UserPlus, type LucideIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { SetupChecklist, SetupItem } from '@/lib/dashboardTypes'
import { cn } from '@/lib/utils'
import { isSetupVisible } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useDismissSetup, useSetupChecklist } from './useDashboardBlocks'

const ITEM_ICON: Record<SetupItem['key'], LucideIcon> = {
  STUDIO: Store,
  BANK: Landmark,
  SERVICE: Tags,
  TASK_TEMPLATE: ListChecks,
  WATERMARK: Stamp,
  CLIENT: UserPlus,
}

/** Presentational first-run checklist: segmented progress, open steps as dashed rows, finished steps as chips. */
export function SetupChecklistView({ checklist, onDismiss, dismissing }: {
  checklist: SetupChecklist
  onDismiss: () => void
  dismissing: boolean
}) {
  const open = checklist.items.filter(i => !i.done)
  const done = checklist.items.filter(i => i.done)
  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <div
          role="progressbar"
          aria-label="Tiến độ thiết lập"
          aria-valuemin={0}
          aria-valuemax={checklist.total}
          aria-valuenow={checklist.doneCount}
          className="flex flex-1 gap-1"
        >
          {Array.from({ length: checklist.total }, (_, i) => (
            <span key={i} className={cn('h-2 flex-1 rounded-full', i < checklist.doneCount ? 'bg-foreground' : 'bg-border')} />
          ))}
        </div>
        <span className="text-xs text-muted-foreground">
          <span className="text-sm font-semibold tabular-nums text-foreground">{checklist.doneCount}/{checklist.total}</span> bước
        </span>
      </div>

      {open.length > 0 && (
        <ul className="space-y-2">
          {open.map(item => {
            const Icon = ITEM_ICON[item.key] ?? ListChecks
            return (
              <li key={item.key}>
                <Link
                  to={item.href}
                  className="flex min-h-11 items-center gap-3 rounded-lg border border-dashed border-border-strong px-3 py-2.5 transition-colors hover:border-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-sm bg-highlight-soft text-highlight-text">
                    <Icon className="h-4 w-4" aria-hidden="true" />
                  </span>
                  <span className="sr-only">Chưa xong: </span>
                  <span className="min-w-0">
                    <span className="block text-sm font-semibold text-foreground">{item.label}</span>
                    <span className="block text-xs text-muted-foreground">{item.hint}</span>
                  </span>
                  <span aria-hidden="true" className="ml-auto hidden shrink-0 rounded-sm border border-border-strong bg-card px-2.5 py-1 text-xs font-semibold sm:inline-block">
                    Thiết lập
                  </span>
                </Link>
              </li>
            )
          })}
        </ul>
      )}

      <div className="flex flex-wrap items-center gap-x-1.5 gap-y-2">
        {done.length > 0 && (
          <ul className="flex flex-wrap gap-1.5">
            {done.map(item => (
              <li key={item.key} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-surface-sunk px-2.5 py-1 text-xs font-medium text-muted-foreground">
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-success" aria-hidden="true" />
                <span className="sr-only">Xong: </span>
                {item.label}
              </li>
            ))}
          </ul>
        )}
        <Button type="button" variant="ghost" size="sm" className="ml-auto text-muted-foreground" onClick={onDismiss} disabled={dismissing}>Ẩn hướng dẫn</Button>
      </div>
    </div>
  )
}

/** First-run card; renders nothing while loading, on error (it is secondary), once complete or dismissed. */
export function SetupChecklistCard() {
  const { data } = useSetupChecklist()
  const dismiss = useDismissSetup()
  if (!data || !isSetupVisible(data)) return null
  return (
    <Section title="Bắt đầu thiết lập" icon={ListChecks} className="h-full">
      <SetupChecklistView checklist={data} onDismiss={() => dismiss.mutate()} dismissing={dismiss.isPending} />
    </Section>
  )
}
