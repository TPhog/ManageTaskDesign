import { billingLabel } from '@/lib/revisionLabels'
import type { Billing } from '@/lib/revisionTypes'

// docs/08 §5: FREE_FIX xám · IN_QUOTA xanh lá · CHARGEABLE cam — always with text (a11y)
const styles: Record<Billing, string> = {
  FREE_FIX: 'bg-slate-100 text-slate-700 ring-slate-200',
  IN_QUOTA: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  CHARGEABLE: 'bg-orange-50 text-orange-800 ring-orange-200',
}

export function BillingChip({ billing, hint }: { billing: Billing; hint?: string }) {
  return (
    <span
      title={hint}
      className={`inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[billing]}`}
    >
      {billingLabel[billing]}
    </span>
  )
}
