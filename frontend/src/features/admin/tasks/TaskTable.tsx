import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import { Link2, ListChecks, Repeat } from 'lucide-react'
import { StatusBadge } from '@/components/StatusBadge'
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

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="whitespace-nowrap border-b bg-muted/50 text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">
            <th scope="col" className="px-4 py-3">Công việc</th>
            {showProject && <th scope="col" className="hidden px-4 py-3 md:table-cell">Dự án</th>}
            <th scope="col" className="px-4 py-3">Trạng thái</th>
            <th scope="col" className="hidden px-4 py-3 sm:table-cell">Ưu tiên</th>
            <th scope="col" className="px-4 py-3">Hạn</th>
            <th scope="col" className="hidden px-4 py-3 lg:table-cell">Hạng mục</th>
            <th scope="col" className="hidden px-4 py-3 text-right sm:table-cell">Checklist</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {tasks.map((t, i) => (
            <Fragment key={t.id}>
              {groups && groups.length > 0 && t.groupKey != null && t.groupKey !== tasks[i - 1]?.groupKey && (
                <tr>
                  <th scope="rowgroup" colSpan={columns} className="bg-muted/50 px-4 py-2 text-left text-xs font-semibold text-foreground">
                    {groupOf(t.groupKey)?.label ?? t.groupKey}
                    <span className="ml-2 tabular-nums text-muted-foreground">{groupOf(t.groupKey)?.count ?? ''}</span>
                  </th>
                </tr>
              )}
              <tr className="transition-colors duration-150 hover:bg-muted/40">
                <td className="px-4 py-3">
                  <button
                    type="button"
                    onClick={() => onOpen(t.id)}
                    className="rounded-sm text-left font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
                      className="rounded-sm text-foreground hover:text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  )
}
