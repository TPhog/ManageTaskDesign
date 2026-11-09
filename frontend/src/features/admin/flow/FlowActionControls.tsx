import { Link } from 'react-router-dom'
import { AlertTriangle, Copy, Info, QrCode } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { FlowAction, FlowWarning } from '@/lib/flowTypes'
import { actionHref, isCopyAction, isDeliverableConfirmKind, isDialogAction, stepHref } from './flowUtils'

/** In-place commands the runner implements (besides dialog and copy actions). */
const RUNNABLE_KINDS = new Set<string>(['SUBMIT_REVIEW', 'CONFIRM_APPROVAL', 'DELIVER', 'RECORD_DEPOSIT'])

interface ActionProps {
  action: FlowAction
  projectId: string
  busy: boolean
  onRun: (action: FlowAction) => void
  /** Outside the project page: dialog steps become links to it (?step=…); review / approve / deliver link to the deliverables tab */
  dialogsAsLinks?: boolean
  /** Small buttons everywhere (dashboard rows) */
  compact?: boolean
  /** The primary step as the vermilion CTA (the ink "Bước tiếp theo" card) */
  highlightPrimary?: boolean
}

/** One flow action, rendered the same way on the project page (NextStepCard) and on the dashboard. */
export function FlowActionButton({ action, projectId, busy, onRun, dialogsAsLinks = false, compact = false, highlightPrimary = false }: ActionProps) {
  const variant = action.primary ? (highlightPrimary ? 'highlight' : 'default') : 'outline'
  const size = action.primary && !compact ? 'default' : 'sm'
  const href = actionHref(action.kind, projectId, action.target)
    ?? (dialogsAsLinks && isDialogAction(action.kind) ? stepHref(projectId, action.kind) : null)
    ?? (dialogsAsLinks && isDeliverableConfirmKind(action.kind) ? `/admin/projects/${projectId}?tab=deliverables` : null)

  if (href) {
    return (
      <Button asChild variant={variant} size={size}>
        <Link to={href}>{action.label}</Link>
      </Button>
    )
  }

  const isCopy = isCopyAction(action.kind)
  // Unknown kind (newer server): never a dead button
  if (!isCopy && !isDialogAction(action.kind) && !RUNNABLE_KINDS.has(action.kind)) return null

  return (
    <span className="inline-flex items-center gap-1">
      <Button type="button" variant={variant} size={size} disabled={busy} onClick={() => onRun(action)}>
        {isCopy && <Copy aria-hidden="true" />}
        {action.label}
      </Button>
      {isCopy && action.target.quotationId && (
        <Button asChild variant="ghost" size="sm">
          <Link to={`/admin/quotations/${action.target.quotationId}`}>
            <QrCode aria-hidden="true" />
            Xem QR
          </Link>
        </Button>
      )}
    </span>
  )
}

/** A flow warning as a chip (F4: never blocking). Icon + text, so meaning never depends on color alone. */
export function FlowWarningChip({ warning }: { warning: FlowWarning }) {
  const Icon = warning.severity === 'warning' ? AlertTriangle : Info
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        warning.severity === 'warning' ? 'bg-warning-soft text-warning' : 'bg-surface-hover text-foreground',
      )}
    >
      <Icon className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      {warning.message}
    </span>
  )
}
