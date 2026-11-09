import { useId } from 'react'
import type { PublicRequestTemplate } from '@/lib/requestTemplateTypes'

interface Props {
  templates: PublicRequestTemplate[]
  /** '' = free text */
  value: string
  onChange: (templateId: string) => void
  disabled?: boolean
  freeTextLabel?: string
}

// the whole card is the touch target (>= 44px); tokens only, so portal and the public form share it
const card =
  'flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border border-border bg-card px-3 py-2.5 text-sm text-foreground has-[:checked]:border-primary has-[:checked]:bg-primary/5'

/** Plan 13: "Loại yêu cầu" — free text first, then the designer's templates. Callers hide it when there is no template. */
export function RequestKindPicker({ templates, value, onChange, disabled, freeTextLabel = 'Góp ý / yêu cầu khác' }: Props) {
  const name = useId()
  const options = [
    { id: '', title: freeTextLabel, subtitle: 'Tự viết như tin nhắn' },
    ...templates.map(t => ({ id: t.id, title: t.name, subtitle: t.description })),
  ]
  return (
    <fieldset className="m-0 min-w-0 space-y-2 border-0 p-0" disabled={disabled}>
      <legend className="mb-2 text-sm font-medium text-foreground">Loại yêu cầu</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {options.map(o => (
          <label key={o.id || 'free'} className={card}>
            <input
              type="radio"
              name={name}
              value={o.id}
              checked={value === o.id}
              onChange={() => onChange(o.id)}
              className="mt-0.5 h-4 w-4 shrink-0 accent-primary"
            />
            <span className="flex min-w-0 flex-col gap-0.5">
              <span className="font-medium">{o.title}</span>
              {o.subtitle && <span className="text-xs text-muted-foreground">{o.subtitle}</span>}
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  )
}
