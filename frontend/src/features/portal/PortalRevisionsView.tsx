import { QuotaMeter } from '@/components/QuotaMeter'
import { StatusBadge } from '@/components/StatusBadge'
import type { PortalRevisions, PortalRound } from '@/lib/portalTypes'
import { cn } from '@/lib/utils'
import { portalBilling, portalWorkStatus } from './labels'
import { hasRounds, roundTitle } from './revisions'

// docs/08 §5: FREE_FIX gray · IN_QUOTA green · CHARGEABLE orange — always next to the text label
const billingClass: Record<string, string> = {
  FREE_FIX: 'bg-slate-100 text-slate-700 ring-slate-200',
  IN_QUOTA: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  CHARGEABLE: 'bg-orange-50 text-orange-800 ring-orange-200',
}

function RoundList({ rounds }: { rounds: PortalRound[] }) {
  return (
    <ol className="mt-2 space-y-3">
      {rounds.map(r => (
        <li key={r.id} className="rounded-lg border bg-background p-3">
          <p className="text-xs font-semibold text-foreground">{roundTitle(r)}</p>
          <ul className="mt-2 space-y-2">
            {r.items.map(i => (
              <li key={i.id} className="flex items-start justify-between gap-3">
                <p
                  className={cn(
                    'min-w-0 whitespace-pre-wrap break-words text-sm text-foreground',
                    i.workStatus === 'CANCELLED' && 'text-muted-foreground line-through',
                  )}
                >
                  {i.description}
                </p>
                <div className="flex shrink-0 flex-col items-end gap-1">
                  <span
                    className={cn(
                      'inline-flex whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
                      billingClass[i.billing] ?? billingClass.FREE_FIX,
                    )}
                  >
                    {portalBilling[i.billing] ?? i.billing}
                  </span>
                  <StatusBadge status={i.workStatus} label={portalWorkStatus[i.workStatus]} />
                </div>
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ol>
  )
}

/** POR-02 / REV-07: the client's revision rounds per "hạng mục" with the free-revision quota. */
export function PortalRevisionsView({ data }: { data: PortalRevisions }) {
  if (!hasRounds(data)) return <p className="mt-2 text-sm text-muted-foreground">Chưa có lần sửa nào.</p>

  return (
    <div className="mt-3 space-y-5">
      {data.deliverables.filter(d => d.rounds.length > 0).map(d => (
        <div key={d.id}>
          <p className="font-medium text-foreground">{d.name}</p>
          {d.includedRounds > 0 ? (
            <div className="mt-1">
              <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="lần sửa miễn phí" />
            </div>
          ) : (
            <p className="mt-1 text-xs text-muted-foreground">Không gồm lần sửa miễn phí</p>
          )}
          <RoundList rounds={d.rounds} />
        </div>
      ))}
      {data.projectRounds.length > 0 && (
        <div>
          <p className="font-medium text-foreground">Chung cho dự án</p>
          <RoundList rounds={data.projectRounds} />
        </div>
      )}
    </div>
  )
}
