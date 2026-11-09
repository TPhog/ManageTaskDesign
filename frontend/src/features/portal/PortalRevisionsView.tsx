import { QuotaMeter } from '@/components/QuotaMeter'
import type { PortalRevisions, PortalRound } from '@/lib/portalTypes'
import { cn } from '@/lib/utils'
import { Kicker, StatusText, type StatusTextProps } from './kit/editorial'
import { portalBilling, portalWorkStatus } from './labels'
import { hasRounds, roundTitle } from './revisions'

// docs/08 §5: FREE_FIX muted · IN_QUOTA moss · CHARGEABLE ochre — always as words, never colour alone
const billingTone: Record<string, StatusTextProps['tone']> = { FREE_FIX: 'muted', IN_QUOTA: 'success', CHARGEABLE: 'warning' }
const workTone: Record<string, StatusTextProps['tone']> = { TODO: 'muted', IN_PROGRESS: 'highlight', DONE: 'success', CANCELLED: 'muted' }

function RoundList({ rounds }: { rounds: PortalRound[] }) {
  return (
    <ol className="m-0 mt-2 list-none space-y-3 p-0">
      {rounds.map(r => (
        <li key={r.id} className="border-l-2 border-border pl-3.5">
          <Kicker>{roundTitle(r)}</Kicker>
          <ul className="m-0 mt-1 list-none p-0 [&>li+li]:border-t [&>li+li]:border-dotted [&>li+li]:border-border">
            {r.items.map(i => (
              <li key={i.id} className="py-2">
                <p
                  className={cn(
                    'min-w-0 whitespace-pre-wrap break-words font-serif text-base leading-snug text-foreground',
                    i.workStatus === 'CANCELLED' && 'text-muted-foreground line-through',
                  )}
                >
                  {i.description}
                </p>
                <div className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5">
                  <StatusText tone={billingTone[i.billing] ?? 'muted'}>{portalBilling[i.billing] ?? i.billing}</StatusText>
                  <StatusText tone={workTone[i.workStatus] ?? 'muted'}>{portalWorkStatus[i.workStatus] ?? i.workStatus}</StatusText>
                </div>
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ol>
  )
}

/** POR-02 / REV-07: the client's revision rounds per product with the free-revision quota (Plan 12 glossary). */
export function PortalRevisionsView({ data }: { data: PortalRevisions }) {
  if (!hasRounds(data)) return <p className="mt-2 font-serif text-base italic text-muted-foreground">Chưa có lượt chỉnh sửa nào.</p>

  return (
    <div className="mt-3 space-y-6">
      {data.deliverables.filter(d => d.rounds.length > 0).map(d => (
        <div key={d.id}>
          <p className="font-display text-[1.0625rem] font-medium text-foreground">{d.name}</p>
          {d.includedRounds > 0 ? (
            <div className="mt-1 max-w-xs">
              <QuotaMeter used={d.usedRounds} total={d.includedRounds} label="lượt chỉnh sửa miễn phí" />
            </div>
          ) : (
            <p className="mt-1 text-xs text-muted-foreground">Không gồm lượt chỉnh sửa miễn phí</p>
          )}
          <RoundList rounds={d.rounds} />
        </div>
      ))}
      {data.projectRounds.length > 0 && (
        <div>
          <p className="font-display text-[1.0625rem] font-medium text-foreground">Chung cho cả dự án</p>
          <RoundList rounds={data.projectRounds} />
        </div>
      )}
    </div>
  )
}
