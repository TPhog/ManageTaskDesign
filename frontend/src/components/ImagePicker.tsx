import { useState } from 'react'
import { ImagePlus } from 'lucide-react'
import { IMAGE_ACCEPT, MAX_IMAGE_MB, validateImageFiles } from '@/lib/images'
import { cn } from '@/lib/utils'

interface Props {
  /** id of the hidden <input type="file"> (e2e uses it with setInputFiles) */
  id: string
  max: number
  onFiles: (files: File[]) => void
  disabled?: boolean
  label?: string
  hint?: string
}

/** Drag & drop or click to pick images; invalid files are listed, valid ones handed to onFiles. */
export function ImagePicker({ id, max, onFiles, disabled, label = 'Chọn ảnh', hint }: Props) {
  const [dragging, setDragging] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  function take(list: FileList | null) {
    if (!list || list.length === 0) return
    const result = validateImageFiles(Array.from(list), max)
    setErrors(result.errors)
    if (result.files.length > 0) onFiles(result.files)
  }

  return (
    <div>
      <label
        htmlFor={id}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={e => { e.preventDefault(); setDragging(false); if (!disabled) take(e.dataTransfer.files) }}
        className={cn(
          'flex cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border border-dashed px-4 py-4 text-center text-sm text-muted-foreground transition-colors hover:bg-muted/50',
          dragging && 'border-primary bg-primary/5',
          disabled && 'pointer-events-none opacity-50',
        )}
      >
        <ImagePlus className="h-5 w-5" aria-hidden="true" />
        <span className="font-medium text-foreground">{label}</span>
        <span className="text-xs">{hint ?? `Kéo thả hoặc bấm để chọn · JPEG, PNG, WebP · tối đa ${max} ảnh, ${MAX_IMAGE_MB} MB/ảnh`}</span>
      </label>
      <input
        id={id}
        type="file"
        accept={IMAGE_ACCEPT}
        multiple={max > 1}
        disabled={disabled}
        className="sr-only"
        onChange={e => { take(e.target.files); e.target.value = '' }}
      />
      {errors.length > 0 && (
        <ul role="alert" className="mt-2 space-y-0.5 text-xs text-destructive">
          {errors.map(m => <li key={m}>{m}</li>)}
        </ul>
      )}
    </div>
  )
}
