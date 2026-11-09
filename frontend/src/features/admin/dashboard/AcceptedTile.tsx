import { Link } from 'react-router-dom'
import { ArrowUpRight, TrendingUp } from 'lucide-react'
import { MoneyText } from '@/components/MoneyText'
import type { Dashboard, MonthTotal } from '@/lib/dashboardTypes'
import { formatVnd } from '@/lib/format'
import { cn } from '@/lib/utils'
import { barPercent, compactVnd, dashboardLinks, monthLabel } from './dashboardUtils'
import { Section } from './DashboardSection'

/** Any month with money stays a visible bar even when another month dwarfs it. */
const MIN_BAR = 12

const rowLink = 'flex min-h-11 items-center justify-between gap-3 border-t border-border text-sm link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

/**
 * DSH-04: one series → one hue (ink on vermilion), no legend (the tile title names it). The running month is a dashed
 * outline (not final yet); only the latest and the best month carry a direct label. Hover shows the exact value;
 * a table carries the numbers for screen readers.
 */
function AcceptedChart({ data }: { data: MonthTotal[] }) {
  const max = Math.max(0, ...data.map(d => d.total))
  const last = data.length - 1
  const best = max > 0 ? data.findIndex(d => d.total === max) : -1
  return (
    <figure data-testid="dsh-chart" className="space-y-2">
      <figcaption className="sr-only">Giá trị đã chốt 6 tháng gần nhất</figcaption>
      <div className="flex h-40 items-end gap-2.5 border-b-[1.5px] border-foreground pt-6" aria-hidden="true">
        {data.map((d, i) => (
          <div key={d.month} className="group relative flex h-full min-w-0 flex-1 flex-col justify-end">
            <span className={cn(
              'pointer-events-none absolute -top-1 z-10 -translate-y-full whitespace-nowrap rounded-sm bg-foreground px-2 py-1 text-xs font-medium text-background opacity-0 shadow-pop transition-opacity group-hover:opacity-100',
              i >= data.length - 2 ? 'right-0' : i < 2 ? 'left-0' : 'left-1/2 -translate-x-1/2',
            )}>
              {monthLabel(d.month)}: {formatVnd(d.total)}
            </span>
            <div
              className={cn(
                'relative w-full rounded-t-[6px]',
                i === last ? 'border-2 border-b-0 border-dashed border-foreground bg-transparent' : 'bg-foreground',
              )}
              style={{ height: `${Math.max(barPercent(d.total, max), d.total > 0 ? MIN_BAR : 0)}%` }}
            >
              {d.total > 0 && (i === last || i === best) && (
                <span className="absolute bottom-full left-1/2 mb-1 -translate-x-1/2 whitespace-nowrap text-[0.6875rem] font-semibold tabular-nums">
                  {compactVnd(d.total)}
                </span>
              )}
            </div>
          </div>
        ))}
      </div>
      <div className="flex gap-2.5 text-xs font-semibold" aria-hidden="true">
        {data.map(d => <span key={d.month} className="min-w-0 flex-1 text-center tabular-nums">{monthLabel(d.month).split('/')[0]}</span>)}
      </div>
      <table className="sr-only">
        <caption>Giá trị đã chốt theo tháng</caption>
        <thead>
          <tr><th scope="col">Tháng</th><th scope="col">Giá trị</th></tr>
        </thead>
        <tbody>
          {data.map(d => (
            <tr key={d.month}><td>{monthLabel(d.month)}</td><td>{formatVnd(d.total)}</td></tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}

/** The vermilion tile: closed value this month, money still to collect, extras, and the 6-month trend. */
export function AcceptedTile({ money, acceptedByMonth }: { money: Dashboard['money']; acceptedByMonth: MonthTotal[] }) {
  return (
    <Section title="Giá trị đã chốt" icon={TrendingUp} tone="highlight" meta="6 tháng" className="flex flex-col">
      <Link
        to={dashboardLinks.acceptedThisMonth}
        className="group block rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span className="flex items-center justify-between gap-2 text-sm font-medium">
          Đã chốt tháng này
          <ArrowUpRight className="h-4 w-4 shrink-0" aria-hidden="true" />
        </span>
        <span data-testid="dsh-acceptedThisMonth" className="mt-1 block break-words font-display text-display-lg font-bold tabular-nums">
          <MoneyText amount={money.acceptedThisMonth} />
        </span>
      </Link>
      <div>
        <Link to={dashboardLinks.outstanding} className={rowLink}>
          Đang chờ thu
          <span data-testid="dsh-outstanding" className="font-semibold tabular-nums"><MoneyText amount={money.outstanding} /></span>
        </Link>
        <Link to={dashboardLinks.additionalThisMonth} className={cn(rowLink, 'border-b')}>
          Phát sinh tháng này
          <span data-testid="dsh-additionalThisMonth" className="font-semibold tabular-nums"><MoneyText amount={money.additionalThisMonth} /></span>
        </Link>
      </div>
      <div className="mt-auto pt-2">
        <AcceptedChart data={acceptedByMonth} />
      </div>
    </Section>
  )
}
