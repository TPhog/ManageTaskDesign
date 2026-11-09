import { ChevronDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { ProjectDetail } from '@/lib/types'
import { projectTransitions } from './projectStatus'
import { useProjectStatusChange } from './useProjectStatusChange'

/** QA D-01: status menu that follows the BR table; reasons and server warnings come from the shared status flow. */
export function ProjectStatusControl({ project }: { project: ProjectDetail }) {
  const change = useProjectStatusChange(project)
  const transitions = projectTransitions[project.status] ?? []
  if (transitions.length === 0) return null

  return (
    <>
      {/* modal={false}: the menu opens dialogs; a modal menu would leave pointer-events locked on <body> */}
      <DropdownMenu modal={false}>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" disabled={change.pending} className="shrink-0">
            Đổi trạng thái
            <ChevronDown aria-hidden="true" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {transitions.map(t => (
            <DropdownMenuItem
              key={t.to}
              className={t.destructive ? 'text-danger focus:text-danger' : undefined}
              onClick={() => change.request(t)}
            >
              {t.label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
      {change.dialogs}
    </>
  )
}
