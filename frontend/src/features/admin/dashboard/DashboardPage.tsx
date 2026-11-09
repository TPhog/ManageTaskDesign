import { useState } from 'react'
import { Link } from 'react-router-dom'
import { FileText, ListChecks, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { Tile } from '@/components/Tile'
import { cn } from '@/lib/utils'
import { AcceptedTile } from './AcceptedTile'
import { ClientActivityBlock } from './ClientActivityBlock'
import { FlowTodoBlock } from './FlowTodoBlock'
import { MoneyInsightsBlock } from './MoneyInsightsBlock'
import { ProjectHealthBlock } from './ProjectHealthBlock'
import { ProjectsTile } from './ProjectsTile'
import { ScheduleBlock } from './ScheduleBlock'
import { SetupChecklistCard } from './SetupChecklistCard'
import { StorageWidget } from './StorageWidget'
import { TodayTile } from './TodayTile'
import { greetingFor, isSetupVisible, weekdayLong, weekSummary } from './dashboardUtils'
import { useDashboard } from './useDashboard'
import { useDashboardSchedule, useSetupChecklist } from './useDashboardBlocks'

/** Grid cell: the tile inside stretches to the row height. */
const cell = 'min-w-0 *:h-full'

/** A bento column: tiles stack, the last one grows so both columns end on the same line (xl). */
const column = 'flex min-w-0 flex-col gap-4 xl:[&>*:last-child]:flex-1'

/**
 * docs/08 §2.2 "/admin" — Studio Bento (P16). xl = the 12-column bento of the mockup:
 * Hôm nay (+ Thiết lập) · Lịch 7 ngày · [Việc cần làm, Sức khỏe] | [Dự án, Đã chốt, Dung lượng, Khách] · Tiền.
 * Below xl everything stacks in that order; the narrow column pairs up from md.
 */
export default function DashboardPage() {
  const { data, isLoading, error, refetch } = useDashboard()
  const { data: schedule } = useDashboardSchedule()
  const { data: setup } = useSetupChecklist()
  /** Local hour once per visit: the greeting must not flip on a re-render */
  const [hour] = useState(() => new Date().getHours())
  const setupVisible = isSetupVisible(setup)
  const ready = !!data && !isLoading && !error

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={schedule ? weekdayLong(schedule.today) : undefined}
        title={<>
          <span className="sr-only">Tổng quan — </span>
          {greetingFor(hour)}.
          {schedule && <span className="font-semibold text-muted-foreground"> {weekSummary(schedule)}</span>}
        </>}
        actions={<>
          <Button asChild variant="outline"><Link to="/admin/quotations"><FileText aria-hidden="true" />Báo giá</Link></Button>
          <Button asChild><Link to="/admin/tasks"><ListChecks aria-hidden="true" />Bảng công việc</Link></Button>
        </>}
      />

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
        <div className={cn(cell, setupVisible ? 'xl:col-span-7' : 'xl:col-span-12')}>
          {isLoading ? <Skeleton className="h-64 w-full rounded-xl" />
            : error || !data ? (
              <Tile as="div" className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                <TriangleAlert className="h-4 w-4 text-warning" aria-hidden="true" />
                Không tải được số liệu tổng quan.
                <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
              </Tile>
            ) : <TodayTile summary={data.actionRequired} wide={!setupVisible} />}
        </div>
        {setupVisible && <div className={cn(cell, 'xl:col-span-5')}><SetupChecklistCard /></div>}

        <div className="min-w-0 xl:col-span-12"><ScheduleBlock /></div>

        <div className={cn(column, 'xl:col-span-8')}>
          <FlowTodoBlock />
          <ProjectHealthBlock />
        </div>
        <div className={cn(column, 'md:grid md:grid-cols-2 xl:col-span-4 xl:flex')}>
          {ready && <ProjectsTile projects={data.projects} />}
          {ready && <AcceptedTile money={data.money} acceptedByMonth={data.acceptedByMonth} />}
          <StorageWidget className="md:col-span-2 xl:col-span-1" />
          <ClientActivityBlock className="md:col-span-2 xl:col-span-1" />
        </div>

        <div className="min-w-0 xl:col-span-12"><MoneyInsightsBlock /></div>
      </div>
    </div>
  )
}
