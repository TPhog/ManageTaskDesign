import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { FlowState } from '@/lib/flowTypes'
import { stageLabel, stepperSummary } from '@/features/admin/flow/flowUtils'

const statusText = { done: ' (đã xong)', current: ' (hiện tại)', upcoming: '' } as const

/** Plan 06b §6: 5 derived stages; on mobile a single line "Giai đoạn 3/5 · Thực hiện". */
export function FlowStepper({ flow }: { flow: FlowState }) {
  return (
    <nav aria-label="Giai đoạn dự án" className="rounded-xl border bg-card px-5 py-4 shadow-card">
      <div className="sm:hidden">
        <p className="text-sm font-semibold text-foreground">{stepperSummary(flow)}</p>
        <div className="mt-2.5 flex gap-1" aria-hidden="true">
          {flow.steps.map(step => (
            <span
              key={step.stage}
              className={cn(
                'h-1.5 flex-1 rounded-full',
                step.status === 'done' && 'bg-primary',
                step.status === 'current' && 'bg-highlight',
                step.status === 'upcoming' && 'bg-border',
              )}
            />
          ))}
        </div>
      </div>
      <ol className="hidden sm:flex">
        {flow.steps.map((step, i) => (
          <li
            key={step.stage}
            aria-current={step.status === 'current' ? 'step' : undefined}
            className="relative flex min-w-0 flex-1 flex-col gap-2 pr-3 last:pr-0"
          >
            {i < flow.steps.length - 1 && (
              <span
                className={cn('absolute left-9 right-1 top-[13px] h-0.5 rounded-full', step.status === 'done' ? 'bg-primary' : 'bg-border')}
                aria-hidden="true"
              />
            )}
            <span
              className={cn(
                'relative flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold',
                step.status === 'done' && 'bg-primary text-primary-foreground',
                step.status === 'current' && 'bg-highlight text-highlight-foreground ring-[5px] ring-highlight-soft',
                step.status === 'upcoming' && 'border-[1.5px] border-border-strong bg-card text-muted-foreground',
              )}
              aria-hidden="true"
            >
              {step.status === 'done' ? <Check className="h-3.5 w-3.5" strokeWidth={2.6} /> : i + 1}
            </span>
            <span
              className={cn(
                'text-sm font-semibold leading-snug',
                step.status === 'upcoming' ? 'text-muted-foreground' : 'text-foreground',
              )}
            >
              {stageLabel[step.stage]}
              <span className="sr-only">{statusText[step.status]}</span>
            </span>
          </li>
        ))}
      </ol>
      {flow.stage === 'CANCELLED' && <p className="mt-2 hidden text-sm text-muted-foreground sm:block">Dự án đã huỷ</p>}
    </nav>
  )
}
