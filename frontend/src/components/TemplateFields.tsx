import type { ReactNode } from 'react'
import { X } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { DateInput } from '@/components/DateInput'
import { ImagePicker } from '@/components/ImagePicker'
import { MAX_IMAGE_MB } from '@/lib/images'
import { SIZE_UNITS, TEMPLATE_LIMITS, type AnswerDraft, type AnswerDrafts, type SizeDraft } from '@/lib/requestTemplates'
import type { SizeUnit, TemplateField } from '@/lib/requestTemplateTypes'

interface Props {
  idPrefix: string
  fields: TemplateField[]
  drafts: AnswerDrafts
  onChange: (key: string, draft: AnswerDraft) => void
  /** keyed by field key (validateDrafts / answerErrorsFromApi) */
  errors?: Record<string, string>
  files: File[]
  onFilesChange: (files: File[]) => void
  /** 0 hides the images question (studio storage nearly full: text only) */
  maxImages: number
  maxImageMb?: number
  disabled?: boolean
}

// >= 44px rows: the whole row is the touch target (portal / phone)
const choiceRow =
  'flex min-h-11 cursor-pointer items-center gap-3 rounded-lg border px-3 py-2 text-sm text-foreground has-[:checked]:border-primary has-[:checked]:bg-primary/5'
const nativeSelect =
  'flex h-11 w-full rounded-md border border-input bg-background px-3 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:text-sm'

/** Plan 13: one control per template question (docs/08). Tokens only, so admin, portal and the public form share it. The server re-checks every answer. */
export function TemplateFields(props: Props) {
  const fields = props.maxImages > 0 ? props.fields : props.fields.filter(f => f.type !== 'IMAGES')
  return (
    <div className="space-y-5">
      {fields.map(f => <Question key={f.key} field={f} {...props} />)}
    </div>
  )
}

function Question({ field, idPrefix, drafts, onChange, errors, files, onFilesChange, maxImages, maxImageMb = MAX_IMAGE_MB, disabled }: Props & { field: TemplateField }) {
  const id = `${idPrefix}-${field.key}`
  const error = errors?.[field.key]
  const helpId = field.help ? `${id}-help` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [helpId, errorId].filter(Boolean).join(' ') || undefined
  const draft = drafts[field.key]
  const set = (d: AnswerDraft) => onChange(field.key, d)
  const title = (
    <>
      {field.label}
      {field.required && <span className="text-destructive" aria-hidden="true"> *</span>}
    </>
  )
  const footer = (
    <>
      {field.help && <p id={helpId} className="text-xs text-muted-foreground">{field.help}</p>}
      {error && <p id={errorId} className="text-sm text-destructive">{error}</p>}
    </>
  )
  const common = { 'aria-invalid': !!error, 'aria-describedby': describedBy, 'aria-required': field.required, disabled }

  switch (field.type) {
    case 'SHORT_TEXT':
    case 'NUMBER':
      return (
        <div className="space-y-2">
          <Label htmlFor={id}>{title}</Label>
          <Input id={id} className="h-11" value={(draft as string) ?? ''} {...common}
            maxLength={field.type === 'NUMBER' ? 19 : TEMPLATE_LIMITS.maxShortText}
            inputMode={field.type === 'NUMBER' ? 'numeric' : undefined}
            onChange={e => set(e.target.value)} />
          {footer}
        </div>
      )
    case 'LONG_TEXT':
      return (
        <div className="space-y-2">
          <Label htmlFor={id}>{title}</Label>
          <Textarea id={id} rows={4} value={(draft as string) ?? ''} maxLength={TEMPLATE_LIMITS.maxLongText} {...common}
            onChange={e => set(e.target.value)} />
          {footer}
        </div>
      )
    case 'DATE':
      return (
        <div className="space-y-2">
          <Label htmlFor={id}>{title}</Label>
          <DateInput id={id} value={(draft as string) ?? ''} onChange={set} invalid={!!error} describedBy={describedBy} disabled={disabled} />
          {footer}
        </div>
      )
    case 'SINGLE_CHOICE':
      return (
        <Choices id={id} invalid={!!error} legend={title} describedBy={describedBy} footer={footer}>
          {field.options.map(o => (
            <label key={o} className={choiceRow}>
              <input type="radio" name={id} value={o} checked={draft === o} disabled={disabled} className="h-4 w-4 accent-primary" onChange={() => set(o)} />
              {o}
            </label>
          ))}
          {!field.required && draft !== '' && (
            <button type="button" className="min-h-11 text-left text-sm text-muted-foreground underline-offset-2 hover:underline" onClick={() => set('')}>
              Bỏ chọn
            </button>
          )}
        </Choices>
      )
    case 'MULTI_CHOICE': {
      const picked = (draft as string[]) ?? []
      return (
        <Choices id={id} invalid={!!error} legend={title} describedBy={describedBy} footer={footer}>
          {field.options.map(o => (
            <label key={o} className={choiceRow}>
              <input type="checkbox" checked={picked.includes(o)} disabled={disabled} className="h-4 w-4 accent-primary"
                onChange={e => set(e.target.checked ? [...picked, o] : picked.filter(p => p !== o))} />
              {o}
            </label>
          ))}
        </Choices>
      )
    }
    case 'SIZE': {
      const s = draft as SizeDraft
      return (
        <Choices id={id} invalid={!!error} legend={title} describedBy={describedBy} footer={footer}>
          {field.options.map(o => (
            <label key={o} className={choiceRow}>
              <input type="radio" name={id} checked={s.mode === 'preset' && s.preset === o} disabled={disabled} className="h-4 w-4 accent-primary"
                onChange={() => set({ ...s, mode: 'preset', preset: o })} />
              {o}
            </label>
          ))}
          <label className={choiceRow}>
            <input type="radio" name={id} checked={s.mode === 'custom'} disabled={disabled} className="h-4 w-4 accent-primary"
              onChange={() => set({ ...s, mode: 'custom', preset: '' })} />
            Tuỳ chỉnh (rộng × cao)
          </label>
          {s.mode === 'custom' && (
            <div className="col-span-full grid grid-cols-[1fr_auto_1fr_6rem] items-center gap-2">
              <Input aria-label="Chiều rộng" inputMode="decimal" className="h-11" value={s.width} disabled={disabled}
                onChange={e => set({ ...s, width: e.target.value })} />
              <span aria-hidden="true">×</span>
              <Input aria-label="Chiều cao" inputMode="decimal" className="h-11" value={s.height} disabled={disabled}
                onChange={e => set({ ...s, height: e.target.value })} />
              <select aria-label="Đơn vị" className={nativeSelect} value={s.unit} disabled={disabled}
                onChange={e => set({ ...s, unit: e.target.value as SizeUnit })}>
                {SIZE_UNITS.map(u => <option key={u} value={u}>{u}</option>)}
              </select>
            </div>
          )}
        </Choices>
      )
    }
    case 'IMAGES':
      return (
        <div className="space-y-2">
          <Label htmlFor={id}>{title}</Label>
          <ImagePicker id={id} max={Math.max(1, maxImages - files.length)} disabled={disabled || files.length >= maxImages}
            onFiles={picked => onFilesChange([...files, ...picked].slice(0, maxImages))}
            label="Thêm ảnh" hint={`Tối đa ${maxImages} ảnh, ${maxImageMb} MB/ảnh — JPG, PNG, WebP`} />
          {files.length > 0 && (
            <ul className="flex flex-wrap gap-1.5" aria-label="Ảnh đã chọn">
              {files.map((f, i) => (
                <li key={`${f.name}-${i}`} className="inline-flex min-h-8 items-center gap-1 rounded-full bg-muted px-2.5 text-xs">
                  {f.name}
                  <button type="button" aria-label={`Bỏ ${f.name}`} className="-my-1.5 -mr-2.5 inline-flex min-h-11 min-w-11 items-center justify-center" onClick={() => onFilesChange(files.filter((_, j) => j !== i))}>
                    <X className="h-3 w-3" aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
          {footer}
        </div>
      )
  }
}

/** The fieldset carries the question id (tabIndex -1) so a form can focus the first unanswered choice question. */
function Choices({ id, invalid, legend, describedBy, footer, children }: {
  id: string; invalid: boolean; legend: ReactNode; describedBy?: string; footer: ReactNode; children: ReactNode
}) {
  return (
    <fieldset id={id} tabIndex={-1} aria-invalid={invalid || undefined} className="scroll-mt-4 space-y-2 focus:outline-none" aria-describedby={describedBy}>
      <legend className="text-sm font-medium text-foreground">{legend}</legend>
      <div className="grid gap-2 sm:grid-cols-2">{children}</div>
      {footer}
    </fieldset>
  )
}
