import type { FlowAction } from '@/lib/flowTypes'
import { isDialogAction, type FlowDialogKind } from './flowUtils'
import { useConfirmApproval, useCopyQuotationText, useDeliverableStatus, useSubmitReview } from './useFlow'

/**
 * The single implementation of in-place flow commands, shared by NextStepCard and the dashboard "Việc cần làm".
 * START_PROJECT / COMPLETE_PROJECT need the project detail and a dialog: they are handed to `onDialog`.
 */
export function useFlowActionRunner(onDialog?: (kind: FlowDialogKind) => void) {
  const submitReview = useSubmitReview()
  const confirmApproval = useConfirmApproval()
  const deliverableStatus = useDeliverableStatus()
  const copyText = useCopyQuotationText()
  const busy = submitReview.isPending || confirmApproval.isPending || deliverableStatus.isPending

  function run(action: FlowAction) {
    if (isDialogAction(action.kind)) {
      onDialog?.(action.kind)
      return
    }
    const deliverableId = action.target.deliverableId
    switch (action.kind) {
      case 'SUBMIT_REVIEW':
        if (deliverableId) submitReview.mutate({ deliverableId })
        break
      case 'CONFIRM_APPROVAL':
        if (deliverableId) confirmApproval.mutate(deliverableId)
        break
      case 'DELIVER':
        if (deliverableId) deliverableStatus.mutate({ deliverableId, to: 'DELIVERED' })
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
