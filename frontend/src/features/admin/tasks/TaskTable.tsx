import { Link } from 'react-router-dom'
import { Link2, ListChecks, Repeat } from 'lucide-react'
import { StatusBadge } from '@/components/StatusBadge'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'
import { cn } from '@/lib/utils'
import type { TaskGroup, TaskListItem } from '@/lib/types'
import { BlockedLine, DueText, PriorityBadge } from './TaskMeta'
import { taskStatusLabel, todayVn } from './taskUtils'

interface Props {
  tasks: TaskListItem[]
  onOpen: (id: string) => void
  /** Cross-project view: adds the project/client column linking to the project's task tab */
  showProject?: boolean
  /** groupBy result: a header row (label + total over all pages) is inserted where the rows' groupKey changes */
  groups?: TaskGroup[]
}

export function TaskTable({ tasks, onOpen, showProject = false, groups }: Props) {
  const today = todayVn()
  const columns = showProject ? 7 : 6
  const groupOf = (key: string) => groups?.find(g => g.key === key)
  // one <tbody> per group, so the rowgroup header really heads its rows; a single body without grouping
  const grouped = !!groups && groups.length > 0
  const runs: { key: string | null; rows: TaskListItem[] }[] = []
  for (const t of tasks) {
    const key = grouped ? t.groupKey ?? null : null
    const last = runs[runs.length - 1]
    if (last && last.key === key) last.rows.push(t)
    else runs.push({ key, rows: [t] })
  }

  return (
    <div className="relative overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className={tableHeadRowClass}>
            <th scope="col" className="px-4 py-3">Công việc</th>
            {showProject && <th scope="col" className="hidden px-4 py-3 md:table-cell">Dự án</th>}
            <th scope="col" className="px-4 py-3">Trạng thái</th>
            <th scope="col" className="hidden px-4 py-3 sm:table-cell">Ưu tiên</th>
            <th scope="col" className="px-4 py-3">Hạn</th>
            <th scope="col" className="hidden px-4 py-3 lg:table-cell">Hạng mục</th>
            <th scope="col" className="hidden px-4 py-3 text-right sm:table-cell">Checklist</th>
          </tr>
        </thead>
        {runs.map((run, r) => (
          <tbody key={`${r}-${run.key ?? ''}`} className={cn(r > 0 && 'border-t')}>
            {run.key != null && (
              <tr>
                <th scope="rowgroup" colSpan={columns} className="border-b bg-surface-sunk px-4 py-2.5 text-left text-sm font-semibold text-foreground">
                  {groupOf(run.key)?.label ?? run.key}
                  <span className="ml-2 rounded-full border border-border-strong bg-card px-2 py-0.5 text-xs font-semibold tabular-nums text-muted-foreground">{groupOf(run.key)?.count ?? ''}</span>
                </th>
              </tr>
            )}
            {run.rows.map(t => (
              <tr key={t.id} className={tableRowClass}>
                <td className="px-4 py-3">
                  <button
                    type="button"
                    onClick={() => onOpen(t.id)}
                    className="link-hover rounded-sm text-left font-semibold text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    {t.title}
                  </button>
                  {t.isRecurring && (
                    <span role="img" title="Công việc lặp lại" aria-label="Công việc lặp lại" className="ml-1.5 inline-flex align-middle text-muted-foreground">
                      <Repeat className="h-3.5 w-3.5" aria-hidden="true" />
                    </span>
                  )}
                  {t.linksCount > 0 && (
                    <span role="img" title={`${t.linksCount} liên kết`} aria-label={`${t.linksCount} liên kết`} className="ml-1.5 inline-flex items-center gap-0.5 align-middle text-xs tabular-nums text-muted-foreground">
                      <Link2 className="h-3.5 w-3.5" aria-hidden="true" />
                      {t.linksCount}
                    </span>
                  )}
                  {t.status === 'BLOCKED' && <BlockedLine task={t} className="mt-0.5" />}
                </td>
                {showProject && (
                  <td className="hidden px-4 py-3 md:table-cell">
                    <Link
                      to={`/admin/projects/${t.projectId}?tab=tasks`}
                      className="link-hover rounded-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      {t.projectName}
                    </Link>
                    <p className="text-xs text-muted-foreground">{t.clientName}</p>
                  </td>
                )}
                <td className="px-4 py-3">
                  <StatusBadge status={t.status} label={taskStatusLabel[t.status]} />
                </td>
                <td className="hidden px-4 py-3 sm:table-cell">
                  <PriorityBadge priority={t.priority} alwaysShow />
                </td>
                <td className="px-4 py-3">
                  <DueText dueDate={t.dueDate} status={t.status} today={today} />
                </td>
                <td className="hidden px-4 py-3 text-muted-foreground lg:table-cell">{t.deliverableName ?? '—'}</td>
                <td className="hidden px-4 py-3 text-right sm:table-cell">
                  {t.checklistTotal > 0 ? (
                    <span className="inline-flex items-center gap-1 text-xs tabular-nums text-muted-foreground">
                      <ListChecks className="h-3.5 w-3.5" aria-hidden="true" />
                      {t.checklistDone}/{t.checklistTotal}
                    </span>
                  ) : (
                    <span className="text-xs text-muted-foreground">—</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        ))}
      </table>
    </div>
  )
}
