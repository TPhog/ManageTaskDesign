import { useState } from 'react'
import type { ApiError } from '@/lib/api'
import type { ProjectDetail } from '@/lib/types'
import { useTaskList } from '@/features/admin/tasks/useTasks'
import { ApproveDialog, DeliverDialog, SubmitReviewDialog } from './DeliverableActionDialogs'
import { flowErrorText, openTaskSummary } from './flowUtils'
import type { DeliverableActionKind } from './useFlowActionRunner'
import { useConfirmApproval, useDeliverableStatus, useSubmitReview } from './useFlow'

/**
 * Review / approve / deliver always ask first (DF-07): one place for the three confirmation dialogs,
 * used by the deliverables tab and by NextStepCard.
 */
export function useDeliverableDialogs(project: ProjectDetail) {
  const [active, setActive] = useState<{ kind: DeliverableActionKind; id: string } | null>(null)
  const [deliverError, setDeliverError] = useState<string>()
  const submitReview = useSubmitReview()
  const confirmApproval = useConfirmApproval()
  const deliverableStatus = useDeliverableStatus()
  // only needed for the submit warning: load once the dialog is first asked for
  const tasks = useTaskList({ projectId: project.id, pageSize: 100 })

  const deliverable = active ? project.deliverables.find(d => d.id === active.id) : undefined
  const close = () => setActive(null)

  function open(kind: DeliverableActionKind, id: string) {
    setDeliverError(undefined)
    setActive({ kind, id })
  }

  const dialogs = (
    <>
      <SubmitReviewDialog
        deliverable={{ id: deliverable?.id ?? '', name: deliverable?.name ?? '' }}
        open={active?.kind === 'submit' && !!deliverable}
        onOpenChange={o => { if (!o) close() }}
        openTasks={openTaskSummary(tasks.data?.items ?? [], deliverable?.id ?? '')}
        pending={submitReview.isPending}
        onConfirm={clientMessage =>
          deliverable && submitReview.mutate({ deliverableId: deliverable.id, clientMessage: clientMessage ?? undefined }, { onSuccess: close })}
      />
      <ApproveDialog
        deliverable={{ id: deliverable?.id ?? '', name: deliverable?.name ?? '', clientDecision: deliverable?.clientDecision ?? null }}
        open={active?.kind === 'approve' && !!deliverable}
        onOpenChange={o => { if (!o) close() }}
        pending={confirmApproval.isPending}
        onConfirm={() => deliverable && confirmApproval.mutate(deliverable.id, { onSuccess: close })}
      />
      <DeliverDialog
        deliverable={{ id: deliverable?.id ?? '', name: deliverable?.name ?? '' }}
        open={active?.kind === 'deliver' && !!deliverable}
        onOpenChange={o => { if (!o) close() }}
        pending={deliverableStatus.isPending}
        serverError={deliverError}
        onConfirm={v =>
          deliverable &&
          deliverableStatus.mutate(
            { deliverableId: deliverable.id, to: 'DELIVERED', ...v },
            {
              onSuccess: close,
              onError: (err: ApiError) => setDeliverError(err.errors?.deliveryUrl?.[0] ?? flowErrorText(err, '')),
            },
          )}
      />
    </>
  )

  return { open, dialogs, pending: submitReview.isPending || confirmApproval.isPending || deliverableStatus.isPending }
}
