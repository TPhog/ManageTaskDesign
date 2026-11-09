import { useState } from 'react'
import { ArrowDown, ArrowUp, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  FIELD_TYPES_WITH_OPTIONS, FIELD_TYPE_LABEL, TEMPLATE_LIMITS, parseOptions,
} from '@/lib/requestTemplates'
import type { TemplateField, TemplateFieldType } from '@/lib/requestTemplateTypes'

interface Props {
  field: TemplateField
  index: number
  count: number
  onChange: (f: TemplateField) => void
  onMove: (delta: -1 | 1) => void
  onRemove: () => void
  /** a second images question is refused by the server */
  imagesTaken: boolean
}

const nativeSelect =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

export function FieldEditor({ field, index, count, onChange, onMove, onRemove, imagesTaken }: Props) {
  const n = index + 1
  const hasOptions = FIELD_TYPES_WITH_OPTIONS.includes(field.type)
  // raw text while typing; parsed (trimmed, de-duplicated) on blur
  const [optionsText, setOptionsText] = useState(field.options.join('\n'))
  const [prevOptions, setPrevOptions] = useState(field.options)
  if (prevOptions !== field.options) {
    setPrevOptions(field.options)
    if (parseOptions(optionsText).join('\n') !== field.options.join('\n')) setOptionsText(field.options.join('\n'))
  }

  function changeType(type: TemplateFieldType) {
    let options = field.options
    if (!FIELD_TYPES_WITH_OPTIONS.includes(type)) options = []
    else if (type !== 'SIZE' && options.length === 0) options = ['Lựa chọn 1', 'Lựa chọn 2']
    onChange({ ...field, type, options })
  }

  return (
    <li className="space-y-3 rounded-lg border bg-card p-3">
      <div className="flex items-center gap-1.5">
        <span className="w-6 shrink-0 text-right text-xs tabular-nums text-muted-foreground" aria-hidden="true">{n}.</span>
        <Input
          aria-label={`Tiêu đề câu hỏi ${n}`}
          value={field.label}
          maxLength={TEMPLATE_LIMITS.maxLabel}
          onChange={e => onChange({ ...field, label: e.target.value })}
        />
        <Button type="button" variant="ghost" size="icon" className="h-8 w-8 shrink-0" aria-label={`Đưa câu hỏi ${n} lên`}
          disabled={index === 0} onClick={() => onMove(-1)}>
          <ArrowUp aria-hidden="true" />
        </Button>
        <Button type="button" variant="ghost" size="icon" className="h-8 w-8 shrink-0" aria-label={`Đưa câu hỏi ${n} xuống`}
          disabled={index === count - 1} onClick={() => onMove(1)}>
          <ArrowDown aria-hidden="true" />
        </Button>
        <Button type="button" variant="ghost" size="icon" className="h-8 w-8 shrink-0 text-muted-foreground hover:text-danger"
          aria-label={`Xoá câu hỏi ${n}`} onClick={onRemove}>
          <X aria-hidden="true" />
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 pl-[1.875rem]">
        <select
          aria-label={`Loại câu hỏi ${n}`}
          className={`${nativeSelect} w-44`}
          value={field.type}
          onChange={e => changeType(e.target.value as TemplateFieldType)}
        >
          {(Object.keys(FIELD_TYPE_LABEL) as TemplateFieldType[]).map(t => (
            <option key={t} value={t} disabled={t === 'IMAGES' && imagesTaken && field.type !== 'IMAGES'}>{FIELD_TYPE_LABEL[t]}</option>
          ))}
        </select>
        <label className="flex min-h-10 cursor-pointer items-center gap-2 text-sm text-foreground">
          <input type="checkbox" className="h-4 w-4" checked={field.required}
            onChange={e => onChange({ ...field, required: e.target.checked })} />
          Bắt buộc<span className="sr-only"> (câu hỏi {n})</span>
        </label>
      </div>

      <div className="space-y-1.5 pl-[1.875rem]">
        <Label htmlFor={`field-help-${index}`} className="text-xs text-muted-foreground">Gợi ý cho khách (tuỳ chọn)</Label>
        <Input
          id={`field-help-${index}`}
          value={field.help ?? ''}
          maxLength={TEMPLATE_LIMITS.maxHelp}
          onChange={e => onChange({ ...field, help: e.target.value === '' ? null : e.target.value })}
        />
      </div>

      {hasOptions && (
        <div className="space-y-1.5 pl-[1.875rem]">
          <Label htmlFor={`field-options-${index}`} className="text-xs text-muted-foreground">
            {field.type === 'SIZE'
              ? 'Kích thước có sẵn — mỗi dòng một kích thước; khách luôn có thể nhập tuỳ chỉnh'
              : 'Lựa chọn — mỗi dòng một lựa chọn'}
          </Label>
          <Textarea
            id={`field-options-${index}`}
            rows={4}
            value={optionsText}
            onChange={e => { setOptionsText(e.target.value); onChange({ ...field, options: parseOptions(e.target.value) }) }}
            onBlur={() => {
              const parsed = parseOptions(optionsText)
              setOptionsText(parsed.join('\n'))
              onChange({ ...field, options: parsed })
            }}
          />
        </div>
      )}
    </li>
  )
}
