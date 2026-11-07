import { formatMoney } from '@/lib/format'

export function MoneyText({ amount }: { amount: number }) {
  return <span className="tabular-nums">{formatMoney(amount)}</span>
}
