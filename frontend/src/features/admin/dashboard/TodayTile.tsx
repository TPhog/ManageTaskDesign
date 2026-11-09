import { StatTile } from '@/components/StatTile'
import { Tile, TileHeader } from '@/components/Tile'
import type { Dashboard } from '@/lib/dashboardTypes'
import { cn } from '@/lib/utils'
import { dashboardLinks } from './dashboardUtils'

/**
 * Hero tile "Hôm nay của bạn": the DSH-01 counters (docs/08 "Cần xử lý") — their only place on the dashboard.
 * Three big tiles + two compact ones beside the setup card; five equal tiles across when the setup card is gone (`wide`).
 * Phone: the overdue tile spans the row, the rest go two by two.
 */
export function TodayTile({ summary, wide }: { summary: Dashboard['actionRequired']; wide: boolean }) {
  const big = cn('min-h-36 sm:col-span-2 sm:min-h-44', wide && 'xl:col-span-1')
  const small = cn('sm:col-span-3', wide && 'xl:col-span-1 xl:min-h-44')
  return (
    <Tile aria-label="Hôm nay của bạn" className="flex h-full flex-col gap-4">
      <TileHeader title="Hôm nay của bạn" />
      <ul aria-label="Cần xử lý" className={cn('grid flex-1 grid-cols-2 gap-3 sm:grid-cols-6 sm:grid-rows-[1fr_auto]', wide && 'xl:grid-cols-5 xl:grid-rows-1')}>
        <li className={cn(big, 'col-span-2')}>
          <StatTile to={dashboardLinks.overdueTasks} label="Việc quá hạn" value={summary.overdueTasks} testId="dsh-overdueTasks" tone={summary.overdueTasks > 0 ? 'hot' : 'default'} />
        </li>
        <li className={big}>
          <StatTile to={dashboardLinks.dueTodayTasks} label="Việc đến hạn hôm nay" value={summary.dueTodayTasks} testId="dsh-dueTodayTasks" />
        </li>
        <li className={big}>
          <StatTile to={dashboardLinks.awaitingQuotations} label="Báo giá chờ phản hồi" value={summary.awaitingQuotations} testId="dsh-awaitingQuotations" />
        </li>
        <li className={small}>
          <StatTile to={dashboardLinks.newRequests} label="Yêu cầu mới" value={summary.newRequests} testId="dsh-newRequests" size={wide ? 'lg' : 'md'} />
        </li>
        <li className={small}>
          <StatTile to={dashboardLinks.expiringSoonQuotations} label="Báo giá hết hạn trong 3 ngày" value={summary.expiringSoonQuotations} testId="dsh-expiringSoonQuotations" tone={summary.expiringSoonQuotations > 0 ? 'hot' : 'default'} size={wide ? 'lg' : 'md'} />
        </li>
      </ul>
    </Tile>
  )
}
