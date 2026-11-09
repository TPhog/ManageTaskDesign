import { useState } from 'react'
import { Check } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { formatDatetime, formatVnd } from '@/lib/format'
import type { PortalQuotationDetail } from '@/lib/portalTypes'
import { canAccept } from './quotation'
import { useAcceptQuotation } from './useQuotationStatements'

/** CF-03 / ADR-012: the client records "Đồng ý" on a live quotation; the designer confirms it. */
export function QuotationDecision({ quotation: q }: { quotation: PortalQuotationDetail }) {
  const [confirming, setConfirming] = useState(false)
  const accept = useAcceptQuotation(q.id)

  if (canAccept(q)) {
    return (
      <div className="border-l-4 border-highlight py-1 pl-5">
        <p className="font-display text-[1.5rem] font-medium leading-tight text-foreground">Bạn đồng ý với báo giá này?</p>
        <p className="mt-2 max-w-[60ch] font-serif text-base leading-relaxed text-foreground">
          Bấm Đồng ý để báo nhà thiết kế bắt đầu. Đồng ý chưa phải là trả tiền — phần chuyển khoản ở bên dưới.
        </p>
        <Button type="button" size="touch" className="mt-4 w-full" onClick={() => setConfirming(true)}>
          <Check aria-hidden="true" />
          Đồng ý báo giá
        </Button>
        <ConfirmDialog
          open={confirming}
          onOpenChange={setConfirming}
          title={`Đồng ý báo giá ${q.snapshot.number}?`}
          description={`Tổng cộng ${formatVnd(q.snapshot.total)}. Nhà thiết kế sẽ xác nhận lại với bạn.`}
          confirmLabel="Đồng ý"
          pending={accept.isPending}
          onConfirm={() => accept.mutate(undefined, { onSettled: () => setConfirming(false) })}
        />
      </div>
    )
  }

  if (q.status === 'SENT' && q.clientAcceptedAt) {
    return (
      <p role="status" className="border-l-4 border-success py-1 pl-5 font-serif text-base italic leading-relaxed text-foreground">
        Bạn đã đồng ý lúc {formatDatetime(q.clientAcceptedAt)} — đang chờ nhà thiết kế xác nhận.
      </p>
    )
  }

  return null
}
