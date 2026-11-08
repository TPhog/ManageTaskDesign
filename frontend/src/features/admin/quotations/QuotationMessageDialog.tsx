import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Link2, MessageSquareText } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { api, type ApiError } from '@/lib/api'
import { quotationSentMessage } from '@/lib/messageTemplates'
import { qk } from '@/lib/queryKeys'
import type { QuotationDetail } from '@/lib/quotationTypes'
import type { ClientDetail, PortalLinkResult, ProjectDetail } from '@/lib/types'

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
  const qc = useQueryClient()
  const [link, setLink] = useState<string | null>(null)

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

  const issue = useMutation({
    mutationFn: () => api.post<PortalLinkResult>(`/admin/clients/${clientId}/portal-link`, {}),
    onSuccess: res => {
      setLink(res.url)
      toast.success('Đã tạo link portal')
      qc.invalidateQueries({ queryKey: ['clients'] })
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể tạo link portal'),
  })

  const s = q.snapshot
  if (!s) return null // every non-DRAFT quotation has a snapshot

  const message = quotationSentMessage(q, link)   // same template as the flow's COPY_QUOTATION_MESSAGE
  const canCreateLink = !link && client !== undefined && !client.hasPortalLink && client.status === 'ACTIVE'

  function handleOpenChange(next: boolean) {
    if (!next) setLink(null) // the raw link is shown once only, like PortalLinkDialog
    onOpenChange(next)
  }

  async function copy(text: string, success: string) {
    try {
      await navigator.clipboard.writeText(text)
      toast.success(success)
    } catch {
      toast.error('Không copy được, hãy chọn và copy thủ công')
    }
  }

  let hint: string | null = null
  if (link) hint = 'Link chỉ hiển thị một lần. Hãy copy và gửi cho khách ngay.'
  else if (client?.hasPortalLink)
    hint = 'Khách đã có link portal (link chỉ hiện một lần lúc tạo), nên tin nhắn nhắc khách mở link cũ. Cần link mới? Vào Khách hàng → Link portal.'
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
            <p className={link ? 'text-xs text-amber-700' : 'text-xs text-muted-foreground'}>{hint}</p>
          ) : null}

          <div className="flex flex-wrap justify-end gap-2">
            {canCreateLink && (
              <Button variant="outline" onClick={() => issue.mutate()} disabled={issue.isPending}>
                <Link2 aria-hidden="true" />
                Tạo link portal
              </Button>
            )}
            {link && (
              <Button variant="outline" onClick={() => copy(link, 'Đã copy link')}>
                <Copy aria-hidden="true" />
                Copy link
              </Button>
            )}
            <Button onClick={() => copy(message, 'Đã copy tin nhắn mẫu')}>
              <MessageSquareText aria-hidden="true" />
              Copy tin nhắn
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
