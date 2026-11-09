import { Link } from 'react-router-dom'
import { FolderKanban } from 'lucide-react'
import { StatTile } from '@/components/StatTile'
import { StatusBadge } from '@/components/StatusBadge'
import type { Dashboard } from '@/lib/dashboardTypes'
import { formatDate } from '@/lib/format'
import { dashboardLinks } from './dashboardUtils'
import { Section } from './DashboardSection'

/** DSH-02: projects in progress / on hold (linked to the filtered list) and the deadlines of the next 7 days. */
export function ProjectsTile({ projects }: { projects: Dashboard['projects'] }) {
  return (
    <Section title="Dự án" icon={FolderKanban}>
      <div className="grid grid-cols-2 gap-3">
        <StatTile to={dashboardLinks.inProgress} label="Đang làm" value={projects.inProgress} testId="dsh-inProgress" size="md" />
        <StatTile to={dashboardLinks.onHold} label="Tạm dừng" value={projects.onHold} testId="dsh-onHold" size="md" />
      </div>
      <div className="space-y-1.5">
        <h3 className="kicker text-muted-foreground">Deadline trong 7 ngày</h3>
        {projects.dueIn7Days.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có dự án nào đến hạn trong 7 ngày tới.</p>
        ) : (
          <ul data-testid="dsh-dueIn7Days" className="divide-y divide-border">
            {projects.dueIn7Days.map(p => (
              <li key={p.id}>
                <Link
                  to={`/admin/projects/${p.id}`}
                  className="flex min-h-11 items-center justify-between gap-3 rounded-md py-2 text-sm link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span className="min-w-0">
                    <span className="block truncate font-semibold text-foreground">{p.name}</span>
                    <span className="block truncate text-xs text-muted-foreground">{p.clientName}</span>
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    <span className="font-medium tabular-nums text-foreground">{formatDate(p.dueDate)}</span>
                    <StatusBadge status={p.status} />
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Section>
  )
}
