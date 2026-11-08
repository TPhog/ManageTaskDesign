import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { FlowState } from '@/lib/flowTypes'
import { stageLabel, stepperSummary } from '@/features/admin/flow/flowUtils'

const statusText = { done: ' (đã xong)', current: ' (hiện tại)', upcoming: '' } as const

/** Plan 06b §6: 5 derived stages; on mobile a single line "Giai đoạn 3/5 · Thực hiện". */
export function FlowStepper({ flow }: { flow: FlowState }) {
  return (
    <nav aria-label="Giai đoạn dự án" className="rounded-xl border bg-card px-4 py-3 shadow-card">
      <p className="text-sm font-medium text-foreground sm:hidden">{stepperSummary(flow)}</p>
      <ol className="hidden items-center gap-2 sm:flex">
        {flow.steps.map((step, i) => (
          <li
            key={step.stage}
            aria-current={step.status === 'current' ? 'step' : undefined}
            className="flex flex-1 items-center gap-2"
          >
            <span
              className={cn(
                'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                step.status === 'done' && 'bg-emerald-500 text-white',
                step.status === 'current' && 'bg-primary text-primary-foreground ring-4 ring-primary/15',
                step.status === 'upcoming' && 'bg-muted text-muted-foreground',
              )}
              aria-hidden="true"
            >
              {step.status === 'done' ? <Check className="h-4 w-4" /> : i + 1}
            </span>
            <span
              className={cn(
                'whitespace-nowrap text-sm',
                step.status === 'current' ? 'font-semibold text-foreground' : 'text-muted-foreground',
              )}
            >
              {stageLabel[step.stage]}
              <span className="sr-only">{statusText[step.status]}</span>
            </span>
            {i < flow.steps.length - 1 && <span className="h-px min-w-4 flex-1 bg-border" aria-hidden="true" />}
          </li>
        ))}
      </ol>
      {flow.stage === 'CANCELLED' && <p className="mt-2 hidden text-sm text-muted-foreground sm:block">Dự án đã huỷ</p>}
    </nav>
  )
}
