import type { FlowAction } from '@/lib/flowTypes'
import { isDialogAction, type FlowDialogKind } from './flowUtils'
import { useConfirmApproval, useCopyQuotationText, useDeliverableStatus, useMarkDeposit, useSubmitReview } from './useFlow'

export type DeliverableActionKind = 'submit' | 'approve' | 'deliver'

/**
 * The single implementation of in-place flow commands, shared by NextStepCard and the dashboard "Việc cần làm".
 * START_PROJECT / COMPLETE_PROJECT / MARK_QUOTED need the project detail and a dialog: they are handed to `onDialog`.
 * With `onDeliverableAction`, review / approve / deliver open a confirmation dialog instead of mutating at once.
 */
export function useFlowActionRunner(
  onDialog?: (kind: FlowDialogKind) => void,
  onDeliverableAction?: (kind: DeliverableActionKind, deliverableId: string) => void,
) {
  const submitReview = useSubmitReview()
  const confirmApproval = useConfirmApproval()
  const deliverableStatus = useDeliverableStatus()
  const markDeposit = useMarkDeposit()
  const copyText = useCopyQuotationText()
  const busy = submitReview.isPending || confirmApproval.isPending || deliverableStatus.isPending || markDeposit.isPending

  function run(action: FlowAction) {
    if (isDialogAction(action.kind)) {
      onDialog?.(action.kind)
      return
    }
    const deliverableId = action.target.deliverableId
    switch (action.kind) {
      case 'SUBMIT_REVIEW':
        if (!deliverableId) break
        if (onDeliverableAction) onDeliverableAction('submit', deliverableId)
        else submitReview.mutate({ deliverableId })
        break
      case 'CONFIRM_APPROVAL':
        if (!deliverableId) break
        if (onDeliverableAction) onDeliverableAction('approve', deliverableId)
        else confirmApproval.mutate(deliverableId)
        break
      case 'DELIVER':
        if (!deliverableId) break
        if (onDeliverableAction) onDeliverableAction('deliver', deliverableId)
        else deliverableStatus.mutate({ deliverableId, to: 'DELIVERED' })
        break
      case 'RECORD_DEPOSIT':
        if (action.target.quotationId) markDeposit.mutate(action.target.quotationId)
        break
      case 'COPY_QUOTATION_MESSAGE':
      case 'COPY_PAYMENT_REMINDER':
        if (action.target.quotationId) void copyText(action.kind, action.target.quotationId)
        break
      default:
        break
    }
  }

  return { run, busy }
}
