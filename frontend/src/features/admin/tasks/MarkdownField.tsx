import { useState } from 'react'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { MarkdownView } from '@/components/MarkdownView'
import { cn } from '@/lib/utils'

interface Props {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  maxLength: number
  placeholder?: string
  error?: string
}

/** Plan 06b F5: Markdown with Xem / Sửa. Starts in edit mode only when empty. */
export function MarkdownField({ id, label, value, onChange, maxLength, placeholder, error }: Props) {
  const [editing, setEditing] = useState(value.trim() === '')
  const modeButton = (edit: boolean, text: string) => (
    <button
      type="button"
      aria-pressed={editing === edit}
      onClick={() => setEditing(edit)}
      className={cn(
        'rounded px-2.5 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        editing === edit ? 'bg-card text-foreground shadow-card' : 'text-muted-foreground hover:text-foreground',
      )}
    >
      {text}
    </button>
  )

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <Label id={`${id}-label`} htmlFor={editing ? id : undefined}>{label}</Label>
        <div role="group" aria-label={`Chế độ ${label.toLowerCase()}`} className="inline-flex rounded-md bg-muted p-0.5">
          {modeButton(false, 'Xem')}
          {modeButton(true, 'Sửa')}
        </div>
      </div>
      {editing ? (
        <>
          <Textarea
            id={id}
            rows={6}
            maxLength={maxLength}
            placeholder={placeholder}
            value={value}
            onChange={e => onChange(e.target.value)}
            aria-invalid={!!error}
            aria-describedby={error ? `${id}-error` : `${id}-hint`}
          />
          <p id={`${id}-hint`} className="text-xs text-muted-foreground">
            Markdown: **đậm**, *nghiêng*, - danh sách, [chữ](https://…) · {value.length}/{maxLength}
          </p>
        </>
      ) : value.trim() ? (
        <div role="region" aria-labelledby={`${id}-label`} className="rounded-md border bg-background p-3">
          <MarkdownView source={value} />
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Chưa có mô tả — bấm Sửa để thêm.</p>
      )}
      {error && <p id={`${id}-error`} className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
