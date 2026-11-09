import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Copy, QrCode, TriangleAlert, Wallet } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { MoneyText } from '@/components/MoneyText'
import { Tile, TileHeader } from '@/components/Tile'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'
import type { MoneyInsights, MoneyPeriod, ReceivableItem } from '@/lib/dashboardTypes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { agingLabel, AGING_BUCKET_LABEL, deltaLabel, lastDayOf, PERIOD_LABEL, receivableReminder } from './dashboardUtils'
import { useMoneyInsights } from './useDashboardBlocks'

const PERIODS: MoneyPeriod[] = ['month', 'quarter', 'year']
const REGION = 'Tiền đã thu và công nợ'

async function copyReminder(item: ReceivableItem) {
  try {
    await navigator.clipboard.writeText(receivableReminder(item))
    toast.success('Đã sao chép tin nhắc thanh toán')
  } catch {
    toast.error('Không sao chép được — hãy mở báo giá để lấy tin nhắn')
  }
}

const valueClass = 'mt-1.5 block break-words font-display text-display-md font-bold tabular-nums'

function Stat({ label, value, note, testId, noteTestId, children }: {
  label: string
  value: ReactNode
  note?: ReactNode
  testId: string
  noteTestId?: string
  children?: ReactNode
}) {
  return (
    <div className="min-w-0 rounded-lg bg-muted p-3.5">
      <span className="block text-xs text-muted-foreground">{label}</span>
      <span data-testid={testId} className={valueClass}>{value}</span>
      {children}
      {note && <span data-testid={noteTestId} className="mt-1 block text-xs text-muted-foreground">{note}</span>}
    </div>
  )
}

/** Deposit vs remainder share of the collected money; nothing to draw when both are 0. */
function SplitBar({ deposits, remainders }: { deposits: number; remainders: number }) {
  const sum = deposits + remainders
  if (sum <= 0) return null
  return (
    <span aria-hidden="true" className="mt-2.5 flex h-1.5 gap-0.5 overflow-hidden rounded-full">
      {deposits > 0 && <span className="rounded-full bg-foreground" style={{ flexGrow: deposits }} />}
      {remainders > 0 && <span className="rounded-full bg-highlight" style={{ flexGrow: remainders }} />}
    </span>
  )
}

const agingChip: Record<ReceivableItem['agingBucket'], string> = {
  '31+': 'bg-danger-soft text-danger',
  '8-30': 'bg-warning-soft text-warning',
  '0-7': 'border border-border text-muted-foreground',
}

function ReceivableRow({ item }: { item: ReceivableItem }) {
  return (
    <tr
      data-testid={`dsh-receivable-${item.quotationId}`}
      className={cn('grid grid-cols-2 gap-x-3 gap-y-1 py-3 sm:grid-cols-4 xl:table-row xl:py-0', tableRowClass)}
    >
      <td className="min-w-0 xl:table-cell xl:py-3 xl:pr-3">
        <span className="block text-xs text-muted-foreground xl:hidden">Khách</span>
        <span className="block truncate text-sm font-semibold text-foreground xl:max-w-52">{item.clientName}</span>
      </td>
      <td className="min-w-0 xl:table-cell xl:py-3 xl:pr-3">
        <span className="block text-xs text-muted-foreground xl:hidden">Dự án</span>
        <Link to={`/admin/projects/${item.projectId}`} className="block truncate text-sm link-hover xl:max-w-64">{item.projectName}</Link>
      </td>
      <td className="xl:table-cell xl:whitespace-nowrap xl:py-3 xl:pr-3 xl:text-right">
        <span className="block text-xs text-muted-foreground xl:hidden">Còn nợ</span>
        <span className="block text-sm font-semibold"><MoneyText amount={item.outstanding} /></span>
      </td>
      <td className="xl:table-cell xl:py-3 xl:pr-3">
        <span className="block text-xs text-muted-foreground xl:hidden">Tuổi nợ</span>
        <span className="flex flex-wrap items-center gap-1.5 text-sm tabular-nums">
          {agingLabel(item.agingDays)}
          <span className={cn('whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold', agingChip[item.agingBucket])}>
            {AGING_BUCKET_LABEL[item.agingBucket]}
          </span>
        </span>
      </td>
      <td className="col-span-2 pt-1 sm:col-span-4 xl:table-cell xl:py-3">
        <div className="flex flex-wrap gap-2 xl:justify-end">
          <Button type="button" variant="outline" size="sm" className="min-h-11 xl:min-h-0" onClick={() => void copyReminder(item)}>
            <Copy className="h-4 w-4" aria-hidden="true" />
            Copy nhắc thanh toán
          </Button>
          <Button asChild variant="outline" size="sm" className="min-h-11 xl:min-h-0">
            <Link to={`/admin/quotations/${item.quotationId}`}>
              <QrCode className="h-4 w-4" aria-hidden="true" />
              Mở báo giá &amp; QR
            </Link>
          </Button>
        </div>
      </td>
    </tr>
  )
}

/**
 * Presentational: an ink band with the period's collected money (vs the previous period), what is still owed and the
 * per-client totals, above a white tile with the receivables ledger (full width, so a row stays on one line).
 */
export function MoneyInsightsView({ insights, period, onPeriodChange, loading = false }: {
  insights: MoneyInsights
  period: MoneyPeriod
  onPeriodChange: (p: MoneyPeriod) => void
  /** The shown numbers belong to the previous period while the new one loads. */
  loading?: boolean
}) {
  const { collected, accepted, receivables } = insights
  const periods = (
    <div role="group" aria-label="Kỳ báo cáo" className="inline-flex rounded-md bg-muted p-[3px]">
      {PERIODS.map(p => (
        <button
          key={p}
          type="button"
          data-testid={`dsh-period-${p}`}
          aria-pressed={period === p}
          onClick={() => onPeriodChange(p)}
          className={cn(
            'min-h-11 rounded-[8px] px-3 text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:min-h-8',
            period === p ? 'bg-foreground text-background' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {PERIOD_LABEL[p]}
        </button>
      ))}
    </div>
  )

  return (
    <div aria-busy={loading} className={cn('grid gap-4 transition-opacity', loading && 'opacity-50')}>
      <Tile as="div" tone="inverse" className="grid gap-x-8 gap-y-5 lg:grid-cols-12">
        <div className="min-w-0 space-y-4 lg:col-span-7 xl:col-span-8">
          <TileHeader title={REGION} icon={Wallet} />
          <div className="flex flex-wrap items-center justify-between gap-2">
            {periods}
            <span className="text-xs tabular-nums text-muted-foreground">
              {formatDate(insights.current.start)} – {formatDate(lastDayOf(insights.current.endExclusive))}
            </span>
          </div>

          <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
            <Stat
              label="Đã thu"
              testId="dsh-collected"
              noteTestId="dsh-collected-delta"
              value={<MoneyText amount={collected.current} />}
              note={deltaLabel(collected.deltaPercent)}
            />
            <div className="tone-highlight min-w-0 rounded-lg p-3.5">
              <span className="block text-xs font-medium">Phải thu</span>
              <span data-testid="dsh-receivables-total" className={valueClass}><MoneyText amount={receivables.total} /></span>
              <span className="mt-1 block text-xs">
                {receivables.items.length + receivables.hiddenItems} khoản đang chờ
              </span>
            </div>
            <Stat
              label="Cọc / Phần còn lại"
              testId="dsh-collected-split"
              value={<span className="text-base">Cọc <MoneyText amount={collected.currentDeposits} /></span>}
              note={<>Phần còn lại <MoneyText amount={collected.currentRemainders} /></>}
            >
              <SplitBar deposits={collected.currentDeposits} remainders={collected.currentRemainders} />
            </Stat>
            <Stat
              label="Đã chốt"
              testId="dsh-accepted"
              value={<MoneyText amount={accepted.current} />}
              note={deltaLabel(accepted.deltaPercent)}
            />
          </div>
        </div>

        {receivables.byClient.length > 0 && (
          <div className="min-w-0 lg:col-span-5 xl:col-span-4">
            <h4 className="kicker mb-2 text-muted-foreground lg:mt-1.5">Tổng theo khách</h4>
            <ul>
              {receivables.byClient.map(c => (
                <li key={c.clientId} className="flex justify-between gap-3 border-t border-border py-2 text-sm">
                  <span className="min-w-0 truncate">{c.clientName}</span>
                  <span className="shrink-0 font-semibold tabular-nums"><MoneyText amount={c.outstanding} /></span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Tile>

      <Tile as="div" className="min-w-0 space-y-3">
        <h3 className="font-display text-title font-bold text-foreground">Công nợ cần thu</h3>

        {receivables.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có khoản nào đang chờ thu.</p>
        ) : (
          <>
            <div className="relative">
              <table className="w-full text-left">
                <caption className="sr-only">Công nợ theo dự án</caption>
                <thead className="sr-only xl:not-sr-only">
                  <tr className={tableHeadRowClass}>
                    <th scope="col" className="py-2.5 pr-3 font-semibold">Khách</th>
                    <th scope="col" className="py-2.5 pr-3 font-semibold">Dự án</th>
                    <th scope="col" className="py-2.5 pr-3 text-right font-semibold">Còn nợ</th>
                    <th scope="col" className="py-2.5 pr-3 font-semibold">Tuổi nợ</th>
                    <th scope="col" className="py-2.5 text-right font-semibold"><span className="sr-only">Thao tác</span></th>
                  </tr>
                </thead>
                <tbody className="block xl:table-row-group">
                  {receivables.items.map(i => <ReceivableRow key={i.quotationId} item={i} />)}
                </tbody>
              </table>
            </div>
            {receivables.hiddenItems > 0 && (
              <Link to="/admin/quotations?status=ACCEPTED" className="inline-block text-sm link-inline">
                và {receivables.hiddenItems} khoản khác
              </Link>
            )}
          </>
        )}
      </Tile>
    </div>
  )
}

export function MoneyInsightsBlock() {
  const [period, setPeriod] = useState<MoneyPeriod>('month')
  const { data, isLoading, isPlaceholderData, error, refetch } = useMoneyInsights(period)
  return (
    <section aria-label={REGION} className="min-w-0">
      {isLoading ? (
        <Tile as="div" className="space-y-3">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-32 w-full" />
        </Tile>
      ) : error || !data ? (
        <Tile as="div" className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
          Không tải được số liệu tiền.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </Tile>
      ) : (
        <MoneyInsightsView insights={data} period={period} onPeriodChange={setPeriod} loading={isPlaceholderData} />
      )}
    </section>
  )
}
