import { useState, type FormEvent } from 'react'
import { CircleCheck, TriangleAlert } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { ApiError } from '@/lib/api'
import type { DigestSettings } from '@/lib/types'
import { useDigestSettings, useUpdateDigestSettings } from './useDigest'
import { useReportDirty } from './settingsForm'
import { SaveBar, SettingsSection } from './SettingsSection'

export function formatHour(h: number): string {
  return `${String(h).padStart(2, '0')}:00`
}

const HOURS = Array.from({ length: 24 }, (_, h) => h)

interface DigestFormProps {
  value: DigestSettings
  onSave: (v: { enabled: boolean; hourVn: number }) => void
  saving: boolean
}

export function DigestForm({ value, onSave, saving }: DigestFormProps) {
  const [enabled, setEnabled] = useState(value.enabled)
  const [hourVn, setHourVn] = useState(value.hourVn)

  const dirty = enabled !== value.enabled || hourVn !== value.hourVn
  useReportDirty('digest', dirty)

  function submit(e: FormEvent) {
    e.preventDefault()
    onSave({ enabled, hourVn })
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-5">
      <label className="flex min-h-11 items-center gap-3 text-sm text-foreground">
        <input
          type="checkbox"
          role="switch"
          aria-label="Gửi nhắc việc mỗi sáng"
          checked={enabled}
          onChange={e => setEnabled(e.target.checked)}
          className="relative h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-muted-foreground/40 transition-colors before:absolute before:left-0.5 before:top-0.5 before:h-4 before:w-4 before:rounded-full before:bg-card before:transition-transform checked:bg-primary checked:before:translate-x-4"
        />
        Gửi nhắc việc mỗi sáng
      </label>
      <div className="space-y-2">
        <Label htmlFor="digest-hour" className="block">Giờ gửi (giờ Việt Nam)</Label>
        <select
          id="digest-hour"
          value={hourVn}
          disabled={!enabled}
          onChange={e => setHourVn(Number(e.target.value))}
          className="h-11 w-full rounded-md border border-input bg-background px-3 text-sm disabled:opacity-50 sm:w-40"
        >
          {HOURS.map(h => <option key={h} value={h}>{formatHour(h)}</option>)}
        </select>
      </div>
      {value.channelConfigured ? (
        <p className="flex items-start gap-2 rounded-md border border-success/30 bg-success-soft px-3 py-2 text-sm text-success">
          <CircleCheck aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />
          Đã kết nối Discord webhook — tin nhắn nhắc việc mỗi sáng được gửi vào kênh đã cấu hình.
        </p>
      ) : (
        <p className="flex items-start gap-2 rounded-md border border-warning/30 bg-warning-soft px-3 py-2 text-sm text-warning">
          <TriangleAlert aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />
          Chưa cấu hình Discord webhook nên chưa có tin nào được gửi đi.
        </p>
      )}
      <SaveBar>
        <Button type="submit" disabled={saving || !dirty} className="min-w-36">
          {saving ? 'Đang lưu…' : 'Lưu'}
        </Button>
      </SaveBar>
    </form>
  )
}

export function DigestSection() {
  const { data, isLoading, isError, refetch } = useDigestSettings()
  const save = useUpdateDigestSettings()

  return (
    <SettingsSection
      id="settings-digest"
      title="Nhắc việc hằng ngày"
      description="Mỗi sáng gửi qua Discord: việc quá hạn, hôm nay, ngày mai, deadline trong 3 ngày, báo giá sắp hết hạn. Không có gì để báo thì không gửi."
    >
      {isLoading ? <Skeleton className="h-32 w-full" /> : isError || !data ? (
        <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
          Không tải được cấu hình nhắc việc.
          <Button type="button" variant="outline" size="sm" onClick={() => refetch()}>Thử lại</Button>
        </div>
      ) : (
        <DigestForm
          key={`${data.enabled}-${data.hourVn}`}
          value={data}
          saving={save.isPending}
          onSave={body => save.mutate(body, {
            onSuccess: () => toast.success('Đã lưu nhắc việc hằng ngày'),
            onError: err => toast.error(Object.values((err as ApiError).errors ?? {})[0]?.[0] ?? 'Không lưu được nhắc việc hằng ngày'),
          })}
        />
      )}
    </SettingsSection>
  )
}
