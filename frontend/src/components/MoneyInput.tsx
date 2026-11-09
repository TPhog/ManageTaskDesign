import { forwardRef, useCallback, useLayoutEffect, useRef, useState, type InputHTMLAttributes } from 'react'
import { Input } from '@/components/ui/input'
import { formatMoneyInput, parseMoneyInput } from '@/lib/moneyInput'

type MoneyInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> & {
  value: number
  onChange: (amount: number) => void
}

/** Caret index in `text` that has `digits` digits to its left. */
function caretAfterDigits(text: string, digits: number): number {
  if (digits <= 0) return 0
  let seen = 0
  for (let i = 0; i < text.length; i++) {
    if (/\d/.test(text[i])) seen++
    if (seen === digits) return i + 1
  }
  return text.length
}

/** Whole-đồng input: the thousands dots show while typing ("2.000.000"), the caret stays on the digit being edited. */
export const MoneyInput = forwardRef<HTMLInputElement, MoneyInputProps>(function MoneyInput(
  { value, onChange, onFocus, onBlur, ...rest },
  forwardedRef,
) {
  const [focused, setFocused] = useState(false)
  const inputRef = useRef<HTMLInputElement | null>(null)
  // digits left of the caret at the last edit; reapplied once React has written the formatted text
  const digitsLeft = useRef<number | null>(null)
  const text = focused && value === 0 ? '' : formatMoneyInput(value)

  const setRefs = useCallback((el: HTMLInputElement | null) => {
    inputRef.current = el
    if (typeof forwardedRef === 'function') forwardedRef(el)
    else if (forwardedRef) forwardedRef.current = el
  }, [forwardedRef])

  useLayoutEffect(() => {
    const el = inputRef.current
    if (digitsLeft.current === null || !el || document.activeElement !== el) return
    const pos = caretAfterDigits(el.value, digitsLeft.current)
    el.setSelectionRange(pos, pos)
    digitsLeft.current = null
  }, [text])

  return (
    <Input
      {...rest}
      ref={setRefs}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      value={text}
      onFocus={e => { setFocused(true); onFocus?.(e) }}
      onBlur={e => { setFocused(false); onBlur?.(e) }}
      onChange={e => {
        const el = e.target
        const caret = el.selectionStart ?? el.value.length
        digitsLeft.current = el.value.slice(0, caret).replace(/\D/g, '').length
        onChange(parseMoneyInput(el.value))
      }}
    />
  )
})
