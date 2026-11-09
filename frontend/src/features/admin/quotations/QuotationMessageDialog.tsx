import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Copy, Link2, MessageSquareText } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { api } from '@/lib/api'
import { usePortalLinkForClient } from '@/lib/portalLink'
import { quotationSentMessage } from '@/lib/messageTemplates'
import { qk } from '@/lib/queryKeys'
import type { QuotationDetail } from '@/lib/quotationTypes'
import type { ClientDetail, ProjectDetail } from '@/lib/types'

interface Props {
  quotation: QuotationDetail
  open: boolean
  onOpenChange: (open: boolean) => void
  /** true right after "Gửi" (docs/08 §2.3 bước 6) */
  justSent: boolean
}

/**
 * docs/08 §2.3 bước 6: "Đã gửi. Sao chép link portal / tin nhắn mẫu". The raw portal link exists only right after it is
 * created (07-security §4.1): it goes into the message only when the designer creates it here (client without a link);
 * otherwise the message asks the client to open the link they already have.
 */
export function QuotationMessageDialog({ quotation: q, open, onOpenChange, justSent }: Props) {
  const [confirming, setConfirming] = useState<'regenerate' | 'close' | null>(null)
  // the raw link only exists right after it is created: has the designer copied it (link or message) yet?
  const [copied, setCopied] = useState(false)

  const { data: project } = useQuery({
    queryKey: qk.projects.detail(q.projectId),
    queryFn: () => api.get<ProjectDetail>(`/admin/projects/${q.projectId}`),
    enabled: open,
  })
  const clientId = project?.client.id
  const { data: client } = useQuery({
    queryKey: qk.clients.detail(clientId ?? ''),
    queryFn: () => api.get<ClientDetail>(`/admin/clients/${clientId}`),
    enabled: open && !!clientId,
  })

  const portal = usePortalLinkForClient(clientId)
  const link = portal.link

  const s = q.snapshot
  if (!s) return null // every non-DRAFT quotation has a snapshot

  const portalOff = !link && !!client?.hasPortalLink && !client.portalEnabled
  const message = portalOff ? '' : quotationSentMessage(q, link)   // same template as the flow's COPY_QUOTATION_MESSAGE
  const canCreateLink = !link && client !== undefined && !client.hasPortalLink && client.status === 'ACTIVE'
  // the client holds a link we can no longer show: offer a new one (the old one stops working)
  const canReplaceLink = !link && client !== undefined && client.hasPortalLink && client.portalEnabled

  function close() {
    portal.clear() // the raw link is shown once only, like PortalLinkDialog
    setCopied(false)
    setConfirming(null)
    onOpenChange(false)
  }

  function handleOpenChange(next: boolean) {
    if (next) onOpenChange(true)
    else if (link && !copied) setConfirming('close')
    else close()
  }

  async function copy(text: string, success: string, holdsLink = !!link): Promise<boolean> {
    try {
      await navigator.clipboard.writeText(text)
      toast.success(success)
      if (holdsLink) setCopied(true)
      return true
    } catch {
      toast.error('Không copy được, hãy chọn và copy thủ công')
      return false
    }
  }

  /** Creates the link, puts it into the message and copies that message in one go. */
  async function createAndCopy() {
    setConfirming(null)
    const url = await portal.create()
    if (!url) return
    setCopied(false)
    await copy(quotationSentMessage(q, url), 'Đã tạo link và sao chép tin nhắn', true)
  }

  let hint: string | null = null
  if (link) hint = 'Link chỉ hiển thị một lần. Hãy copy và gửi cho khách ngay.'
  else if (client?.hasPortalLink && !client.portalEnabled)
    hint = 'Portal của khách đang tắt — bật lại ở Khách hàng trước khi gửi tin nhắn.'
  else if (client?.hasPortalLink)
    hint = 'Khách đã có link portal (link chỉ hiện một lần lúc tạo), nên tin nhắn nhắc khách mở link cũ. Muốn chèn link vào tin nhắn? Tạo link mới — link cũ sẽ ngừng dùng được.'
  else if (canCreateLink) hint = 'Khách chưa có link portal. Tạo link để khách xem báo giá và quét QR.'

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{justSent ? 'Đã gửi báo giá' : 'Tin nhắn mẫu'}</DialogTitle>
          <DialogDescription>Sao chép link portal / tin nhắn mẫu để gửi cho khách qua Zalo hoặc email.</DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="quotation-message">Tin nhắn gửi khách</Label>
            <Textarea
              id="quotation-message"
              readOnly
              rows={4}
              value={message}
              onFocus={e => e.currentTarget.select()}
            />
          </div>

          {!client && !link ? (
            <Skeleton className="h-4 w-2/3" />
          ) : hint ? (
            <p className={link || portalOff ? 'text-xs text-amber-700' : 'text-xs text-muted-foreground'}>{hint}</p>
          ) : null}

          <div className="flex flex-wrap justify-end gap-2">
            {canCreateLink && (
              <Button variant="outline" onClick={() => void createAndCopy()} disabled={portal.isPending}>
                <Link2 aria-hidden="true" />
                Tạo link portal
              </Button>
            )}
            {canReplaceLink && (
              <Button variant="outline" onClick={() => setConfirming('regenerate')} disabled={portal.isPending}>
                <Link2 aria-hidden="true" />
                Tạo link mới và chèn vào tin nhắn
              </Button>
            )}
            {link && (
              <Button variant="outline" onClick={() => void copy(link, 'Đã copy link')}>
                <Copy aria-hidden="true" />
                Copy link
              </Button>
            )}
            <Button onClick={() => void copy(message, 'Đã copy tin nhắn mẫu')} disabled={portalOff}>
              <MessageSquareText aria-hidden="true" />
              Copy tin nhắn
            </Button>
          </div>
        </div>
      </DialogContent>

      <ConfirmDialog
        open={confirming === 'regenerate'}
        onOpenChange={o => { if (!o) setConfirming(null) }}
        title="Tạo link mới?"
        description="Link cũ khách đang giữ sẽ ngừng dùng được. Tiếp tục?"
        confirmLabel="Tạo link mới"
        pending={portal.isPending}
        onConfirm={() => void createAndCopy()}
      />
      <ConfirmDialog
        open={confirming === 'close'}
        onOpenChange={o => { if (!o) close() }}
        title="Đóng mà chưa sao chép?"
        description="Link chỉ hiển thị một lần. Đóng mà chưa sao chép?"
        confirmLabel="Sao chép rồi đóng"
        cancelLabel="Đóng"
        onConfirm={async () => { if (await copy(message, 'Đã copy tin nhắn mẫu')) close() }}
      />
    </Dialog>
  )
}
