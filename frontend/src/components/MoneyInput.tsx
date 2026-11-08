import { useState, type InputHTMLAttributes } from 'react'
import { Input } from '@/components/ui/input'
import { formatMoneyInput, parseMoneyInput } from '@/lib/moneyInput'

type MoneyInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> & {
  value: number
  onChange: (amount: number) => void
}

/** QA D-12: whole-đồng input — raw digits while typing, '2.000.000' once the field loses focus. */
export function MoneyInput({ value, onChange, onFocus, onBlur, ...rest }: MoneyInputProps) {
  const [focused, setFocused] = useState(false)
  return (
    <Input
      {...rest}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      value={focused ? (value === 0 ? '' : String(value)) : formatMoneyInput(value)}
      onFocus={e => { setFocused(true); onFocus?.(e) }}
      onBlur={e => { setFocused(false); onBlur?.(e) }}
      onChange={e => onChange(parseMoneyInput(e.target.value))}
    />
  )
}
