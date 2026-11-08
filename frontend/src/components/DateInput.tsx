import { useEffect, useId, useRef, useState } from 'react'
import { CalendarDays } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { isoToVn, vnToIso, weekdayLabel } from '@/lib/dateInput'
import { cn } from '@/lib/utils'

interface DateInputProps {
  id: string
  /** 'YYYY-MM-DD' or '' */
  value: string
  onChange: (iso: string) => void
  invalid?: boolean
  describedBy?: string
  disabled?: boolean
  className?: string
}

/**
 * QA D-05: a day-first dd/mm/yyyy text input that does not depend on the browser locale.
 * The calendar button opens the native picker of a hidden <input type="date">.
 */
export function DateInput({ id, value, onChange, invalid, describedBy, disabled, className }: DateInputProps) {
  const [text, setText] = useState(() => isoToVn(value))
  const [bad, setBad] = useState(false)
  const picker = useRef<HTMLInputElement>(null)
  const hintId = useId()

  // Follow outside changes (form reset, picker) unless the text already means that date
  useEffect(() => {
    setText(t => (vnToIso(t) === value || (t.trim() === '' && value === '') ? t : isoToVn(value)))
    setBad(false)
  }, [value])

  function handleText(raw: string) {
    setText(raw)
    if (raw.trim() === '') {
      setBad(false)
      if (value !== '') onChange('')
      return
    }
    const iso = vnToIso(raw)
    if (iso) {
      setBad(false)
      if (iso !== value) onChange(iso)
    }
  }

  function handleBlur() {
    if (text.trim() === '') return
    const iso = vnToIso(text)
    if (iso) setText(isoToVn(iso))
    else setBad(true)
  }

  function openPicker() {
    const el = picker.current
    if (!el) return
    try {
      el.showPicker()
    } catch {
      el.focus()
    }
  }

  const hint = bad ? 'Ngày không hợp lệ — nhập theo dạng dd/mm/yyyy' : value ? weekdayLabel(value) : ''
  const described = [describedBy, hint ? hintId : undefined].filter(Boolean).join(' ') || undefined

  return (
    <div className={cn('space-y-1', className)}>
      <div className="relative">
        <Input
          id={id}
          value={text}
          placeholder="dd/mm/yyyy"
          inputMode="numeric"
          autoComplete="off"
          maxLength={10}
          disabled={disabled}
          aria-invalid={invalid || bad || undefined}
          aria-describedby={described}
          onChange={e => handleText(e.target.value)}
          onBlur={handleBlur}
          className="pr-11"
        />
        <button
          type="button"
          onClick={openPicker}
          disabled={disabled}
          aria-label="Chọn ngày từ lịch"
          className="absolute right-0 top-0 inline-flex h-10 w-10 items-center justify-center rounded-md text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <CalendarDays className="h-4 w-4" aria-hidden="true" />
        </button>
        <input
          ref={picker}
          type="date"
          tabIndex={-1}
          aria-hidden="true"
          className="pointer-events-none absolute bottom-0 right-0 h-0 w-0 opacity-0"
          value={value}
          onChange={e => onChange(e.target.value)}
        />
      </div>
      {hint && <p id={hintId} className={cn('text-xs', bad ? 'text-destructive' : 'text-muted-foreground')}>{hint}</p>}
    </div>
  )
}
