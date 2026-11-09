import { useState, type KeyboardEvent } from 'react'
import { ArrowDown, ArrowUp, Plus, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { MAX_STEP_LENGTH, MAX_TEMPLATE_STEPS, addStep, moveStep } from './templateUtils'

interface Props {
  value: string[]
  onChange: (steps: string[]) => void
  error?: string | null
}

/** Plan 06b F3: ordered step names; "Bắt đầu dự án" proposes one task per step for deliverables of this service. */
export function TaskTemplateEditor({ value, onChange, error }: Props) {
  const [draft, setDraft] = useState('')
  const full = value.length >= MAX_TEMPLATE_STEPS

  function add() {
    const next = addStep(value, draft)
    if (next === value) return
    onChange(next)
    setDraft('')
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return
    e.preventDefault() // do not submit the service form
    add()
  }

  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-medium text-foreground">Bước mẫu khi bắt đầu dự án</legend>
      <p className="text-xs text-muted-foreground">
        Mỗi bước thành một công việc của hạng mục dùng dịch vụ này. Tối đa {MAX_TEMPLATE_STEPS} bước.
      </p>
      {value.length > 0 && (
        <ol className="space-y-1.5">
          {value.map((step, i) => (
            <li key={i} className="flex items-center gap-1.5">
              <span className="w-5 shrink-0 text-right text-xs tabular-nums text-muted-foreground">{i + 1}.</span>
              <Input
                aria-label={`Bước ${i + 1}`}
                value={step}
                maxLength={MAX_STEP_LENGTH}
                onChange={e => onChange(value.map((s, j) => (j === i ? e.target.value : s)))}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0"
                aria-label={`Đưa bước ${i + 1} lên`}
                disabled={i === 0}
                onClick={() => onChange(moveStep(value, i, -1))}
              >
                <ArrowUp aria-hidden="true" />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0"
                aria-label={`Đưa bước ${i + 1} xuống`}
                disabled={i === value.length - 1}
                onClick={() => onChange(moveStep(value, i, 1))}
              >
                <ArrowDown aria-hidden="true" />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0 text-muted-foreground hover:bg-danger-soft hover:text-danger"
                aria-label={`Xoá bước ${i + 1}`}
                onClick={() => onChange(value.filter((_, j) => j !== i))}
              >
                <X aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ol>
      )}
      <div className="flex gap-2">
        <Label htmlFor="template-new-step" className="sr-only">Bước mới</Label>
        <Input
          id="template-new-step"
          placeholder="VD: Phác thảo"
          value={draft}
          maxLength={MAX_STEP_LENGTH}
          disabled={full}
          onChange={e => setDraft(e.target.value)}
          onKeyDown={onKeyDown}
        />
        <Button type="button" variant="outline" disabled={full || !draft.trim()} onClick={add}>
          <Plus aria-hidden="true" />
          Thêm bước
        </Button>
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </fieldset>
  )
}
