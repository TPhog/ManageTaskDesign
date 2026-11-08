import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { ApiError } from '@/lib/api'
import { useUpdateWatermark, useWatermark } from './useStorage'

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

  return (
    <section aria-labelledby="settings-watermark" className="grid gap-6 rounded-xl border bg-card p-6 shadow-card md:grid-cols-3">
      <div>
        <h2 id="settings-watermark" className="font-semibold text-foreground">Watermark ảnh</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Khách chỉ xem bản có watermark. Thay đổi chỉ áp dụng cho ảnh tải lên sau khi lưu.
        </p>
      </div>
      {isLoading ? <Skeleton className="h-40 w-full md:col-span-2" /> : isError ? (
        <p className="text-sm text-muted-foreground md:col-span-2">Không tải được cấu hình watermark.</p>
      ) : (
        <form onSubmit={submit} noValidate className="space-y-5 md:col-span-2">
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)} className="h-4 w-4 accent-primary" />
            Bật watermark cho ảnh gửi khách
          </label>
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
            className="relative h-28 overflow-hidden rounded-lg bg-gradient-to-br from-slate-300 to-slate-500"
          >
            {enabled && (
              <div className="absolute inset-[-50%] flex rotate-[-30deg] flex-wrap content-center gap-x-10 gap-y-6 text-xs font-bold text-white"
                style={{ opacity: opacity / 100 }}>
                {Array.from({ length: 24 }, (_, i) => <span key={i}>{sample}</span>)}
              </div>
            )}
          </div>
          <div className="flex justify-end">
            <Button type="submit" variant="outline" disabled={save.isPending}>
              {save.isPending ? 'Đang lưu…' : 'Lưu watermark'}
            </Button>
          </div>
        </form>
      )}
    </section>
  )
}
