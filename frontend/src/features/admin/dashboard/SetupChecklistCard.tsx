import { Link } from 'react-router-dom'
import { CheckCircle2, Circle, ListChecks } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { SetupChecklist } from '@/lib/dashboardTypes'
import { cn } from '@/lib/utils'
import { Section } from './DashboardSection'
import { useDismissSetup, useSetupChecklist } from './useDashboardBlocks'

/** Presentational first-run checklist: progress + one row per setup step. */
export function SetupChecklistView({ checklist, onDismiss, dismissing }: {
  checklist: SetupChecklist
  onDismiss: () => void
  dismissing: boolean
}) {
  const percent = checklist.total === 0 ? 0 : Math.round((checklist.doneCount / checklist.total) * 100)
  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <div
          role="progressbar"
          aria-label="Tiến độ thiết lập"
          aria-valuemin={0}
          aria-valuemax={checklist.total}
          aria-valuenow={checklist.doneCount}
          className="h-2 flex-1 overflow-hidden rounded-full bg-muted"
        >
          <div className="h-full bg-primary" style={{ width: `${percent}%` }} />
        </div>
        <span className="text-sm font-medium tabular-nums text-foreground">{checklist.doneCount}/{checklist.total}</span>
      </div>
      <ul className="divide-y">
        {checklist.items.map(item => {
          const body = (
            <>
              {item.done
                ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" aria-hidden="true" />
                : <Circle className="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />}
              <span className="sr-only">{item.done ? 'Xong: ' : 'Chưa xong: '}</span>
              <span className="min-w-0">
                <span className={cn('block text-sm font-medium', item.done ? 'text-muted-foreground line-through' : 'text-foreground')}>{item.label}</span>
                <span className="block text-xs text-muted-foreground">{item.hint}</span>
              </span>
            </>
          )
          return (
            <li key={item.key}>
              {item.done ? (
                <div className="flex min-h-11 items-start gap-3 py-2">{body}</div>
              ) : (
                <Link
                  to={item.href}
                  className="flex min-h-11 items-start gap-3 rounded-md py-2 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {body}
                </Link>
              )}
            </li>
          )
        })}
      </ul>
      <Button type="button" variant="ghost" size="sm" onClick={onDismiss} disabled={dismissing}>Ẩn hướng dẫn</Button>
    </div>
  )
}

/** First-run card; renders nothing while loading, on error (it is secondary), once complete or dismissed. */
export function SetupChecklistCard() {
  const { data } = useSetupChecklist()
  const dismiss = useDismissSetup()
  if (!data || data.dismissed || data.complete) return null
  return (
    <Section title="Bắt đầu thiết lập" icon={ListChecks}>
      <SetupChecklistView checklist={data} onDismiss={() => dismiss.mutate()} dismissing={dismiss.isPending} />
    </Section>
  )
}
