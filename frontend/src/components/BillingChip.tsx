import { billingLabel } from '@/lib/revisionLabels'
import type { Billing } from '@/lib/revisionTypes'

// docs/08 §5: FREE_FIX xám · IN_QUOTA xanh lá · CHARGEABLE cam — always with text (a11y)
const styles: Record<Billing, string> = {
  FREE_FIX: 'bg-surface-sunk text-muted-foreground ring-border',
  IN_QUOTA: 'bg-success-soft text-success ring-success/25',
  CHARGEABLE: 'bg-warning-soft text-warning ring-warning/25',
}

export function BillingChip({ billing, hint }: { billing: Billing; hint?: string }) {
  return (
    <span
      title={hint}
      className={`inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset editorial:rounded-none editorial:bg-transparent editorial:px-0 editorial:ring-0 editorial:italic editorial:font-serif ${styles[billing]}`}
    >
      {billingLabel[billing]}
    </span>
  )
}
