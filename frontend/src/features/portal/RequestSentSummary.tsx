import type { ReactNode } from 'react'
import { CircleCheck } from 'lucide-react'
import type { SentRequestSummary } from './PortalRequestForm'
import { usePortalMe } from './usePortal'

interface Props {
  summary: SentRequestSummary
  deliverableName: string | null
  actions: ReactNode
}

/** CF-13: after sending — the request quoted back, its photos, the free revisions left and when to expect an answer. */
export function RequestSentSummary({ summary, deliverableName, actions }: Props) {
  const { data: me } = usePortalMe()
  const { remainingRounds } = summary
  const meta = [deliverableName ?? 'Cả dự án', summary.imageCount > 0 ? `${summary.imageCount} ảnh đính kèm` : null]
    .filter(Boolean)
    .join(' · ')

  return (
    <div role="status" className="border-t-[3px] border-rule pt-4">
      <div className="flex items-center gap-2.5">
        <CircleCheck className="h-6 w-6 shrink-0 text-success" aria-hidden="true" />
        <p className="font-display text-[1.5rem] font-medium leading-tight text-foreground">Đã gửi yêu cầu</p>
      </div>
      <blockquote className="m-0 mt-4 line-clamp-4 whitespace-pre-wrap break-words border-l-2 border-border pl-3.5 font-serif text-base italic leading-relaxed text-foreground">
        {summary.request.content}
      </blockquote>
      <p className="mt-2 text-xs text-muted-foreground">{meta}</p>
      <div className="mt-4 space-y-1.5 border-y border-border py-3 text-sm text-foreground">
        <p>{`Nhà thiết kế thường phản hồi ${me?.designer.responseTime || 'trong 24 giờ'}.`}</p>
        {remainingRounds !== null && (
          <p>
            {remainingRounds > 0
              ? `Nếu yêu cầu này dùng lượt miễn phí, bạn còn ${remainingRounds - 1} lượt chỉnh sửa miễn phí.`
              : 'Nhà thiết kế sẽ báo giá trước khi sửa vì bạn đã dùng hết lượt miễn phí.'}
          </p>
        )}
      </div>
      {actions && <div className="mt-4 flex flex-col gap-2 sm:flex-row">{actions}</div>}
    </div>
  )
}
