import { Link } from 'react-router-dom'
import { ListChecks } from 'lucide-react'
import { StatusBadge } from '@/components/StatusBadge'
import type { TaskListItem } from '@/lib/types'
import { DueText, PriorityBadge } from './TaskMeta'
import { taskStatusLabel, todayVn } from './taskUtils'

interface Props {
  tasks: TaskListItem[]
  onOpen: (id: string) => void
  /** Cross-project view: adds the project/client column linking to the project's task tab */
  showProject?: boolean
}

export function TaskTable({ tasks, onOpen, showProject = false }: Props) {
  const today = todayVn()

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
          {tasks.map(t => (
            <tr key={t.id} className="transition-colors duration-150 hover:bg-muted/40">
              <td className="px-4 py-3">
                <button
                  type="button"
                  onClick={() => onOpen(t.id)}
                  className="rounded-sm text-left font-medium text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {t.title}
                </button>
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
                <PriorityBadge priority={t.priority} />
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
      </table>
    </div>
  )
}
