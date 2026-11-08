import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AlertTriangle, Info } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { cn } from '@/lib/utils'
import type { ProjectDetail } from '@/lib/types'
import type { FlowState } from '@/lib/flowTypes'
import { FlowActionButton } from '@/features/admin/flow/FlowActionControls'
import { flowActionKey, stepFromParam, type FlowDialogKind } from '@/features/admin/flow/flowUtils'
import { useCompleteProject } from '@/features/admin/flow/useFlow'
import { useFlowActionRunner } from '@/features/admin/flow/useFlowActionRunner'
import { StartProjectDialog } from './StartProjectDialog'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

interface Props {
  project: ProjectDetail
  flow: FlowState | undefined
  isLoading: boolean
  isError: boolean
}

/** Plan 06b F1: one primary next step, warnings (never blocking, F4) and secondary links. BR-01: every step is a click. */
export function NextStepCard({ project, flow, isLoading, isError }: Props) {
  const [startOpen, setStartOpen] = useState(false)
  const [completeOpen, setCompleteOpen] = useState(false)
  const complete = useCompleteProject(project.id)
  const openDialog = (kind: FlowDialogKind) => (kind === 'START_PROJECT' ? setStartOpen(true) : setCompleteOpen(true))
  const runner = useFlowActionRunner(openDialog)
  const busy = runner.busy || complete.isPending
  const [searchParams, setSearchParams] = useSearchParams()
  const step = stepFromParam(searchParams.get('step'))

  // Dashboard "Việc cần làm" links dialog steps as ?step=…: open the dialog once if the flow still offers it, then drop the param
  useEffect(() => {
    if (!step || !flow) return
    if (flow.actions.some(a => a.kind === step)) {
      if (step === 'START_PROJECT') setStartOpen(true)
      else setCompleteOpen(true)
    }
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      next.delete('step')
      return next
    }, { replace: true })
  }, [step, flow, setSearchParams])

  if (isLoading) {
    return (
      <div className={panelClass}>
        <Skeleton className="h-5 w-40" />
        <Skeleton className="mt-3 h-10 w-56" />
      </div>
    )
  }
  if (isError || !flow) {
    return (
      <div className={panelClass}>
        <p className="text-sm text-muted-foreground">Không tải được bước tiếp theo.</p>
      </div>
    )
  }

  const [primary, ...others] = flow.actions
  const completeWarning = flow.warnings.map(w => w.message).join(' · ')

  return (
    <section aria-labelledby="next-step-heading" className={panelClass}>
      <h2 id="next-step-heading" className="text-sm font-semibold text-foreground">Bước tiếp theo</h2>

      {flow.warnings.length > 0 && (
        <ul aria-label="Cảnh báo" className="mt-3 space-y-2">
          {flow.warnings.map((w, i) => (
            <li
              key={`${w.kind}-${i}`}
              className={cn(
                'flex items-start gap-2 rounded-lg px-3 py-2 text-sm',
                w.severity === 'warning' ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-700',
              )}
            >
              {w.severity === 'warning'
                ? <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                : <Info className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />}
              {w.message}
            </li>
          ))}
        </ul>
      )}

      {flow.stage === 'CANCELLED' ? (
        <p className="mt-3 text-sm text-muted-foreground">Dự án đã huỷ — không có bước tiếp theo.</p>
      ) : !primary ? (
        <p className="mt-3 text-sm text-muted-foreground">
          {project.status === 'COMPLETED' ? 'Dự án đã hoàn tất.' : 'Chưa có việc cần làm ngay — đang chờ khách phản hồi.'}
        </p>
      ) : (
        <>
          <div className="mt-4">
            <FlowActionButton action={primary} projectId={project.id} busy={busy} onRun={runner.run} />
          </div>
          {others.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              {others.map(a => (
                <FlowActionButton key={flowActionKey(a)} action={a} projectId={project.id} busy={busy} onRun={runner.run} />
              ))}
            </div>
          )}
        </>
      )}

      <StartProjectDialog project={project} open={startOpen} onOpenChange={setStartOpen} warnings={flow.warnings} />
      <ConfirmDialog
        open={completeOpen}
        onOpenChange={setCompleteOpen}
        title="Hoàn thành dự án?"
        description={
          completeWarning
            ? `Lưu ý: ${completeWarning}. Hệ thống chỉ cảnh báo, không chặn.`
            : 'Dự án sẽ chuyển sang Hoàn thành.'
        }
        confirmLabel="Hoàn thành"
        pending={complete.isPending}
        onConfirm={() => complete.mutate({ rowVersion: project.rowVersion }, { onSuccess: () => setCompleteOpen(false) })}
      />
    </section>
  )
}
