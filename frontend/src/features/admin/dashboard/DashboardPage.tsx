import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, FileText, FolderKanban, TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { MoneyText } from '@/components/MoneyText'
import { PageHeader } from '@/components/PageHeader'
import { StatusBadge } from '@/components/StatusBadge'
import type { MonthTotal } from '@/lib/dashboardTypes'
import { formatDate, formatVnd } from '@/lib/format'
import { barPercent, dashboardLinks, monthLabel } from './dashboardUtils'
import { panelClass, Section } from './DashboardSection'
import { FlowTodoBlock } from './FlowTodoBlock'
import { ScheduleBlock } from './ScheduleBlock'
import { SetupChecklistCard } from './SetupChecklistCard'
import { useDashboard } from './useDashboard'

/** One metric; the whole tile links to the filtered list (docs/08 §2.2). */
function StatTile({ to, label, value, testId }: { to: string; label: string; value: ReactNode; testId: string }) {
  return (
    <Link
      to={to}
      className="group flex flex-col rounded-lg border p-3 transition-colors duration-150 hover:border-primary/40 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="flex items-center justify-between gap-2 text-sm text-muted-foreground">
        {label}
        <ArrowRight className="h-4 w-4 shrink-0 opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100" aria-hidden="true" />
      </span>
      <span data-testid={testId} className="mt-1 text-2xl font-semibold tabular-nums text-foreground">{value}</span>
    </Link>
  )
}

/** DSH-04: one series → one hue, no legend (the caption names it); hover shows the value; a table carries the numbers for screen readers. */
function AcceptedChart({ data }: { data: MonthTotal[] }) {
  const max = Math.max(0, ...data.map(d => d.total))
  return (
    <figure data-testid="dsh-chart" className="space-y-2">
      <figcaption className="text-sm font-medium text-foreground">Giá trị đã chốt 6 tháng gần nhất</figcaption>
      <div className="flex h-36 items-end gap-2 border-b" aria-hidden="true">
        {data.map(d => (
          <div key={d.month} className="group relative flex h-full flex-1 flex-col justify-end">
            <span className="pointer-events-none absolute left-1/2 top-0 z-10 -translate-x-1/2 -translate-y-full whitespace-nowrap rounded-md bg-foreground px-2 py-1 text-xs text-background opacity-0 shadow transition-opacity group-hover:opacity-100">
              {monthLabel(d.month)}: {formatVnd(d.total)}
            </span>
            <div className="mx-auto w-full max-w-10 rounded-t-[4px] bg-primary" style={{ height: `${barPercent(d.total, max)}%` }} />
          </div>
        ))}
      </div>
      <div className="flex gap-2 text-xs text-muted-foreground" aria-hidden="true">
        {data.map(d => <span key={d.month} className="flex-1 text-center tabular-nums">{monthLabel(d.month)}</span>)}
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

/** docs/08 §2.2 "/admin" + spec 06b §6: Việc cần làm (DSH-01 + flow next steps) · Dự án (DSH-02) · Tiền (DSH-03/04). */
export default function DashboardPage() {
  const { data, isLoading, error, refetch } = useDashboard()

  return (
    <div className="space-y-6">
      <PageHeader title="Tổng quan" description="Việc cần làm theo từng dự án, dự án sắp đến hạn và tiền đang chờ thu." />

      {isLoading ? (
        <div className="space-y-6">
          <Skeleton className="h-64 w-full rounded-xl" />
          <div className="grid gap-6 lg:grid-cols-2">
            {[0, 1].map(i => <Skeleton key={i} className="h-72 w-full rounded-xl" />)}
          </div>
        </div>
      ) : error || !data ? (
        <div className={`${panelClass} flex flex-wrap items-center gap-2 text-sm text-muted-foreground`}>
          <TriangleAlert className="h-4 w-4 text-amber-600" aria-hidden="true" />
          Không tải được số liệu tổng quan.
          <Button variant="link" className="px-0" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <div className="space-y-6">
          <SetupChecklistCard />
          <FlowTodoBlock summary={data.actionRequired} />
          <ScheduleBlock />

          <div className="grid gap-6 lg:grid-cols-2">
            <Section title="Dự án" icon={FolderKanban}>
              <div className="grid grid-cols-2 gap-3">
                <StatTile to={dashboardLinks.inProgress} label="Đang làm" value={data.projects.inProgress} testId="dsh-inProgress" />
                <StatTile to={dashboardLinks.onHold} label="Tạm dừng" value={data.projects.onHold} testId="dsh-onHold" />
              </div>
              <div className="space-y-2">
                <h3 className="text-sm font-medium text-foreground">Deadline trong 7 ngày</h3>
                {data.projects.dueIn7Days.length === 0 ? (
                  <p className="text-sm text-muted-foreground">Không có dự án nào đến hạn trong 7 ngày tới.</p>
                ) : (
                  <ul data-testid="dsh-dueIn7Days" className="divide-y">
                    {data.projects.dueIn7Days.map(p => (
                      <li key={p.id}>
                        <Link
                          to={`/admin/projects/${p.id}`}
                          className="flex items-center justify-between gap-3 rounded-md py-2 text-sm hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          <span className="min-w-0">
                            <span className="block truncate font-medium text-foreground">{p.name}</span>
                            <span className="block truncate text-xs text-muted-foreground">{p.clientName}</span>
                          </span>
                          <span className="flex shrink-0 flex-col items-end gap-1">
                            <span className="tabular-nums text-foreground">{formatDate(p.dueDate)}</span>
                            <StatusBadge status={p.status} />
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </Section>

            <Section title="Tiền" icon={FileText}>
              <div className="grid gap-3">
                <StatTile to={dashboardLinks.outstanding} label="Đang chờ thu" value={<MoneyText amount={data.money.outstanding} />} testId="dsh-outstanding" />
                <div className="grid grid-cols-2 gap-3">
                  <StatTile to={dashboardLinks.acceptedThisMonth} label="Đã chốt tháng này" value={<MoneyText amount={data.money.acceptedThisMonth} />} testId="dsh-acceptedThisMonth" />
                  <StatTile to={dashboardLinks.additionalThisMonth} label="Phát sinh tháng này" value={<MoneyText amount={data.money.additionalThisMonth} />} testId="dsh-additionalThisMonth" />
                </div>
              </div>
              <AcceptedChart data={data.acceptedByMonth} />
            </Section>
          </div>
        </div>
      )}
    </div>
  )
}
