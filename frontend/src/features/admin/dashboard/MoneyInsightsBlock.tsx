import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Copy, QrCode, TriangleAlert, Wallet } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { MoneyText } from '@/components/MoneyText'
import type { MoneyInsights, MoneyPeriod, ReceivableItem } from '@/lib/dashboardTypes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { agingLabel, AGING_BUCKET_LABEL, deltaLabel, lastDayOf, PERIOD_LABEL, receivableReminder } from './dashboardUtils'
import { Section } from './DashboardSection'
import { useMoneyInsights } from './useDashboardBlocks'

const PERIODS: MoneyPeriod[] = ['month', 'quarter', 'year']

async function copyReminder(item: ReceivableItem) {
  try {
    await navigator.clipboard.writeText(receivableReminder(item))
    toast.success('Đã sao chép tin nhắc thanh toán')
  } catch {
    toast.error('Không sao chép được — hãy mở báo giá để lấy tin nhắn')
  }
}

function Stat({ label, value, note, testId, noteTestId }: {
  label: string
  value: React.ReactNode
  note?: React.ReactNode
  testId: string
  noteTestId?: string
}) {
  return (
    <div className="min-w-0 rounded-lg border p-3">
      <span className="block text-sm text-muted-foreground">{label}</span>
      <span data-testid={testId} className="mt-1 block truncate text-2xl font-semibold tabular-nums text-foreground">{value}</span>
      {note && <span data-testid={noteTestId} className="mt-1 block text-xs text-muted-foreground">{note}</span>}
    </div>
  )
}

function ReceivableRow({ item }: { item: ReceivableItem }) {
  return (
    <tr
      data-testid={`dsh-receivable-${item.quotationId}`}
      className="grid grid-cols-2 gap-x-3 gap-y-1 py-3 md:table-row md:py-0"
    >
      <td className="min-w-0 md:table-cell md:py-3 md:pr-3">
        <span className="block text-xs text-muted-foreground md:hidden">Khách</span>
        <span className="block truncate text-sm font-medium text-foreground">{item.clientName}</span>
      </td>
      <td className="min-w-0 md:table-cell md:py-3 md:pr-3">
        <span className="block text-xs text-muted-foreground md:hidden">Dự án</span>
        <Link to={`/admin/projects/${item.projectId}`} className="block truncate text-sm hover:text-primary">{item.projectName}</Link>
      </td>
      <td className="md:table-cell md:py-3 md:pr-3 md:text-right">
        <span className="block text-xs text-muted-foreground md:hidden">Còn nợ</span>
        <span className="block text-sm font-medium"><MoneyText amount={item.outstanding} /></span>
      </td>
      <td className="md:table-cell md:py-3 md:pr-3">
        <span className="block text-xs text-muted-foreground md:hidden">Tuổi nợ</span>
        <span className="flex flex-wrap items-center gap-1.5 text-sm tabular-nums">
          {agingLabel(item.agingDays)}
          <span
            className={cn(
              'rounded-full border px-2 py-0.5 text-xs',
              item.agingBucket === '31+' ? 'border-red-300 bg-red-50 text-red-700'
                : item.agingBucket === '8-30' ? 'border-amber-300 bg-amber-50 text-amber-800'
                  : 'text-muted-foreground',
            )}
          >
            {AGING_BUCKET_LABEL[item.agingBucket]}
          </span>
        </span>
      </td>
      <td className="col-span-2 md:table-cell md:py-3">
        <div className="flex flex-wrap gap-2 md:justify-end">
          <Button type="button" variant="outline" size="sm" className="min-h-11 md:min-h-0" onClick={() => void copyReminder(item)}>
            <Copy className="h-4 w-4" aria-hidden="true" />
            Copy nhắc thanh toán
          </Button>
          <Button asChild variant="outline" size="sm" className="min-h-11 md:min-h-0">
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

/** Presentational: collected money vs the previous period, plus the real receivables list. */
export function MoneyInsightsView({ insights, period, onPeriodChange, loading = false }: {
  insights: MoneyInsights
  period: MoneyPeriod
  onPeriodChange: (p: MoneyPeriod) => void
  /** The shown numbers belong to the previous period while the new one loads. */
  loading?: boolean
}) {
  const { collected, accepted, receivables } = insights
  return (
    <div aria-busy={loading} className={cn('space-y-5 transition-opacity', loading && 'opacity-50')}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div role="group" aria-label="Kỳ báo cáo" className="inline-flex rounded-lg border p-0.5">
          {PERIODS.map(p => (
            <button
              key={p}
              type="button"
              data-testid={`dsh-period-${p}`}
              aria-pressed={period === p}
              onClick={() => onPeriodChange(p)}
              className={cn(
                'min-h-11 rounded-md px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:min-h-9',
                period === p ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {PERIOD_LABEL[p]}
            </button>
          ))}
        </div>
        <span className="text-xs tabular-nums text-muted-foreground">
          {formatDate(insights.current.start)} – {formatDate(lastDayOf(insights.current.endExclusive))}
        </span>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <Stat
          label="Đã thu"
          testId="dsh-collected"
          noteTestId="dsh-collected-delta"
          value={<MoneyText amount={collected.current} />}
          note={deltaLabel(collected.deltaPercent)}
        />
        <Stat
          label="Cọc / Phần còn lại"
          testId="dsh-collected-split"
          value={<span className="text-base">Cọc <MoneyText amount={collected.currentDeposits} /></span>}
          note={<>Phần còn lại <MoneyText amount={collected.currentRemainders} /></>}
        />
        <Stat
          label="Đã chốt"
          testId="dsh-accepted"
          value={<MoneyText amount={accepted.current} />}
          note={deltaLabel(accepted.deltaPercent)}
        />
      </div>

      <div className="space-y-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-sm font-semibold text-foreground">Công nợ cần thu</h3>
          <span className="text-sm text-muted-foreground">
            Tổng <span data-testid="dsh-receivables-total" className="font-semibold text-foreground"><MoneyText amount={receivables.total} /></span>
          </span>
        </div>

        {receivables.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có khoản nào đang chờ thu.</p>
        ) : (
          <>
            <table className="w-full text-left">
              <caption className="sr-only">Công nợ theo dự án</caption>
              <thead className="sr-only md:not-sr-only">
                <tr className="border-b text-xs text-muted-foreground">
                  <th scope="col" className="py-2 pr-3 font-medium">Khách</th>
                  <th scope="col" className="py-2 pr-3 font-medium">Dự án</th>
                  <th scope="col" className="py-2 pr-3 text-right font-medium">Còn nợ</th>
                  <th scope="col" className="py-2 pr-3 font-medium">Tuổi nợ</th>
                  <th scope="col" className="py-2 text-right font-medium"><span className="sr-only">Thao tác</span></th>
                </tr>
              </thead>
              <tbody className="block divide-y md:table-row-group">
                {receivables.items.map(i => <ReceivableRow key={i.quotationId} item={i} />)}
              </tbody>
            </table>
            {receivables.hiddenItems > 0 && (
              <Link to="/admin/quotations?status=ACCEPTED" className="inline-block text-sm text-primary underline-offset-2 hover:underline">
                và {receivables.hiddenItems} khoản khác
              </Link>
            )}
          </>
        )}

        {receivables.byClient.length > 0 && (
          <div className="space-y-1.5">
            <h4 className="text-xs font-medium text-muted-foreground">Tổng theo khách</h4>
            <ul className="flex flex-wrap gap-2">
              {receivables.byClient.map(c => (
                <li key={c.clientId} className="max-w-full truncate rounded-full border px-3 py-1 text-sm">
                  {c.clientName}: <span className="font-medium"><MoneyText amount={c.outstanding} /></span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}

export function MoneyInsightsBlock() {
  const [period, setPeriod] = useState<MoneyPeriod>('month')
  const { data, isLoading, isPlaceholderData, error, refetch } = useMoneyInsights(period)
  return (
    <Section title="Tiền đã thu và công nợ" icon={Wallet}>
      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : error || !data ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được số liệu tiền.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <MoneyInsightsView insights={data} period={period} onPeriodChange={setPeriod} loading={isPlaceholderData} />
      )}
    </Section>
  )
}
