import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { useIntakeLink, useRegenerateIntakeLink, useUpdateIntakeLink } from '@/features/admin/request-templates/useRequestTemplates'

/** Plan 13: turn the public "new client" form on/off, copy its link, or replace the link. */
export function IntakeLinkSection() {
  const link = useIntakeLink()
  const update = useUpdateIntakeLink()
  const regenerate = useRegenerateIntakeLink()
  const [confirming, setConfirming] = useState(false)

  const data = link.data
  const toggle = (enabled: boolean) =>
    update.mutate(enabled, {
      onSuccess: () => toast.success(enabled ? 'Đã bật form' : 'Đã tắt form — link trả về "không còn hoạt động"'),
      onError: () => toast.error('Không đổi được cài đặt. Vui lòng thử lại.'),
    })

  async function copy(url: string) {
    try {
      await navigator.clipboard.writeText(url)
      toast.success('Đã sao chép link')
    } catch {
      toast.error('Không sao chép được — hãy chọn link và sao chép thủ công.')
    }
  }

  return (
    <section aria-labelledby="intake-title" className="grid gap-6 rounded-xl border bg-card p-6 shadow-card md:grid-cols-3">
      <div>
        <h2 id="intake-title" className="font-semibold text-foreground">Form nhận yêu cầu khách mới</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Gửi link này lên Facebook/Zalo để khách mới tự điền yêu cầu theo mẫu. Hồ sơ vào mục Khách mới.
        </p>
      </div>
      <div className="space-y-5 md:col-span-2">
        {link.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : link.isError || !data ? (
          <p role="alert" className="text-sm text-destructive">Không tải được cài đặt form.</p>
        ) : (
          <>
            <label className="flex min-h-11 items-center gap-3 text-sm text-foreground">
              <input
                type="checkbox"
                role="switch"
                aria-label="Bật form khách mới"
                checked={data.enabled}
                disabled={update.isPending}
                onChange={e => toggle(e.target.checked)}
                className="relative h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-muted-foreground/40 transition-colors before:absolute before:left-0.5 before:top-0.5 before:h-4 before:w-4 before:rounded-full before:bg-white before:transition-transform checked:bg-primary checked:before:translate-x-4"
              />
              Bật form khách mới
            </label>
            {data.url && (
              <div className="space-y-2">
                <Label htmlFor="intake-url">Link form</Label>
                <div className="flex flex-wrap gap-2">
                  <Input id="intake-url" readOnly value={data.url} onFocus={e => e.currentTarget.select()} className="min-w-0 flex-1 basis-56" />
                  <Button type="button" variant="outline" onClick={() => copy(data.url!)}>Sao chép</Button>
                  <Button asChild variant="outline">
                    <a href={data.url} target="_blank" rel="noreferrer">Mở form</a>
                  </Button>
                </div>
                {!data.enabled && (
                  <p className="text-xs text-muted-foreground">Đang tắt — khách mở link sẽ thấy "Link không còn hoạt động".</p>
                )}
              </div>
            )}
            <div className="flex flex-wrap items-center justify-between gap-3">
              <Link to="/admin/settings/request-templates" className="text-sm font-medium text-foreground underline-offset-4 hover:underline">
                Quản lý mẫu yêu cầu →
              </Link>
              {data.slug && (
                <Button type="button" variant="outline" size="sm" onClick={() => setConfirming(true)}>Tạo link mới</Button>
              )}
            </div>
          </>
        )}
      </div>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Tạo link mới?"
        description="Link cũ ngừng hoạt động ngay, kể cả bài đăng đã chia sẻ."
        confirmLabel="Tạo link mới"
        pending={regenerate.isPending}
        onConfirm={() => regenerate.mutate(undefined, {
          onSuccess: () => { setConfirming(false); toast.success('Đã tạo link mới') },
          onError: () => toast.error('Không tạo được link mới. Vui lòng thử lại.'),
        })}
      />
    </section>
  )
}
