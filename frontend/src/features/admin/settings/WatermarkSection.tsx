import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { ApiError } from '@/lib/api'
import { useUpdateWatermark, useWatermark } from './useStorage'
import { useReportDirty } from './settingsForm'
import { SaveBar, SettingsSection } from './SettingsSection'

const MIN = 10
const MAX = 40
const MAX_TEXT = 100

/** Spec §4: on/off, text (blank = studio name · Bản xem trước), opacity 10–40 %. Applies to new uploads only. */
export function WatermarkSection() {
  const { data, isLoading, isError } = useWatermark()
  const save = useUpdateWatermark()
  const [enabled, setEnabled] = useState(true)
  const [text, setText] = useState('')
  const [opacity, setOpacity] = useState(18)

  useEffect(() => {
    if (!data) return
    setEnabled(data.enabled)
    setText(data.text ?? '')
    setOpacity(data.opacityPercent)
  }, [data])

  function submit(e: FormEvent) {
    e.preventDefault()
    save.mutate({ enabled, text: text.trim() || null, opacityPercent: opacity }, {
      onSuccess: () => toast.success('Đã lưu watermark — áp dụng cho ảnh tải lên từ bây giờ'),
      onError: err => toast.error(Object.values((err as ApiError).errors ?? {})[0]?.[0] ?? 'Không lưu được watermark'),
    })
  }

  const sample = text.trim() || data?.effectiveText || ''
  useReportDirty('watermark', !!data && (enabled !== data.enabled || text.trim() !== (data.text ?? '').trim() || opacity !== data.opacityPercent))

  return (
    <SettingsSection
      id="settings-watermark"
      title="Watermark ảnh"
      description="Khách chỉ xem bản có watermark. Thay đổi chỉ áp dụng cho ảnh tải lên sau khi lưu."
    >
      {isLoading ? <Skeleton className="h-40 w-full" /> : isError ? (
        <p className="text-sm text-muted-foreground">Không tải được cấu hình watermark.</p>
      ) : (
        <form onSubmit={submit} noValidate className="space-y-5">
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)} className="h-4 w-4 accent-primary" />
            Bật watermark cho ảnh gửi khách
          </label>
          {!enabled && (
            <p role="alert" className="rounded-md border border-warning/30 bg-warning-soft px-3 py-2 text-sm text-warning">
              Khách sẽ nhận ảnh gốc độ phân giải đầy đủ, không watermark
            </p>
          )}
          <div className="space-y-2">
            <Label htmlFor="watermark-text">Nội dung</Label>
            <Input
              id="watermark-text"
              maxLength={MAX_TEXT}
              value={text}
              placeholder={data?.effectiveText}
              onChange={e => setText(e.target.value)}
              disabled={!enabled}
            />
            <p className="text-xs text-muted-foreground">Để trống = “{data?.effectiveText}”.</p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="watermark-opacity">Độ đậm: {opacity}%</Label>
            <input
              id="watermark-opacity"
              type="range"
              min={MIN}
              max={MAX}
              step={1}
              value={opacity}
              disabled={!enabled}
              onChange={e => setOpacity(Number(e.target.value))}
              className="w-full accent-primary"
            />
          </div>
          <div
            aria-hidden="true"
            className="relative h-28 overflow-hidden rounded-lg bg-linear-to-br from-border-strong to-muted-foreground"
          >
            {enabled && (
              <div className="absolute inset-[-50%] flex rotate-[-30deg] flex-wrap content-center gap-x-10 gap-y-6 text-xs font-bold text-card"
                style={{ opacity: opacity / 100 }}>
                {Array.from({ length: 24 }, (_, i) => <span key={i}>{sample}</span>)}
              </div>
            )}
          </div>
          {/* stays enabled when unchanged: saving as is confirms the review (setup checklist WATERMARK) */}
          <SaveBar>
            <Button type="submit" disabled={save.isPending} className="min-w-36">
              {save.isPending ? 'Đang lưu…' : 'Lưu watermark'}
            </Button>
          </SaveBar>
        </form>
      )}
    </SettingsSection>
  )
}
