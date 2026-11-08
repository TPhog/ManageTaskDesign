import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Copy, ExternalLink, MessageSquareText, TriangleAlert } from 'lucide-react'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/StatusBadge'
import { api, type ApiError } from '@/lib/api'
import { formatDatetime } from '@/lib/format'
import { portalInviteMessage, portalLinkState } from '@/lib/portalLink'
import { qk } from '@/lib/queryKeys'
import type { ClientDetail, PortalLinkResult } from '@/lib/types'

interface Props {
  /** null = closed */
  client: { id: string; name: string } | null
  onClose: () => void
}

type Confirming = 'regenerate' | 'revoke' | null

export function PortalLinkDialog({ client, onClose }: Props) {
  const qc = useQueryClient()
  // The raw link lives only in memory right after create/regenerate: the server keeps just its hash.
  const [link, setLink] = useState<string | null>(null)
  const [confirming, setConfirming] = useState<Confirming>(null)

  const { data: detail, isLoading } = useQuery({
    queryKey: qk.clients.detail(client?.id ?? ''),
    queryFn: () => api.get<ClientDetail>(`/admin/clients/${client!.id}`),
    enabled: client !== null,
  })

  // C-04: the client sees this phone/email on the portal — warn before sending a link without any
  const { data: settings } = useQuery({
    queryKey: qk.settings,
    queryFn: () => api.get<{ contactPhone: string | null; contactEmail: string | null }>('/admin/settings'),
    enabled: client !== null,
  })
  const noContact = !!settings && !settings.contactPhone && !settings.contactEmail

  const refresh = () => qc.invalidateQueries({ queryKey: ['clients'] })

  const issueMutation = useMutation({
    mutationFn: () => api.post<PortalLinkResult>(`/admin/clients/${client!.id}/portal-link`, {}),
    onSuccess: res => {
      toast.success(detail?.hasPortalLink ? 'Đã tạo link mới, link cũ đã vô hiệu' : 'Đã tạo link portal')
      setLink(res.url)
      setConfirming(null)
      refresh()
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể tạo link portal'),
  })

  const revokeMutation = useMutation({
    mutationFn: () => api.post(`/admin/clients/${client!.id}/portal/revoke`, {}),
    onSuccess: () => {
      toast.success('Đã thu hồi link portal')
      setLink(null)
      setConfirming(null)
      refresh()
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể thu hồi link'),
  })

  function handleOpenChange(open: boolean) {
    if (open) return
    setLink(null)
    setConfirming(null)
    onClose()
  }

  async function copy(text: string, success: string) {
    try {
      await navigator.clipboard.writeText(text)
      toast.success(success)
    } catch {
      toast.error('Không copy được, hãy chọn và copy thủ công')
    }
  }

  const state = detail ? portalLinkState(detail) : null
  const archived = detail?.status === 'INACTIVE'
  const busy = issueMutation.isPending || revokeMutation.isPending

  return (
    <Dialog open={client !== null} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Link portal</DialogTitle>
          <DialogDescription>
            {client?.name} mở link này để theo dõi tiến độ dự án, không cần đăng nhập.
          </DialogDescription>
        </DialogHeader>

        {isLoading || !detail || !state ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <span className="text-muted-foreground">Trạng thái:</span>
              <StatusBadge status={state.badge} label={state.label} />
              {detail.portalTokenCreatedAt && (
                <span className="text-xs text-muted-foreground">Tạo lúc {formatDatetime(detail.portalTokenCreatedAt)}</span>
              )}
            </div>

            {link ? (
              <div className="space-y-2 rounded-lg border bg-muted/40 p-3">
                <Label htmlFor="portal-link">Link gửi khách</Label>
                <Input
                  id="portal-link"
                  readOnly
                  value={link}
                  onFocus={e => e.currentTarget.select()}
                  className="font-mono text-xs"
                />
                <p className="flex items-start gap-1.5 text-xs text-amber-700">
                  <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                  Link chỉ hiển thị một lần. Hãy copy và gửi cho khách ngay.
                </p>
                <div className="space-y-1.5">
                  <Label htmlFor="portal-invite-preview">Tin nhắn mẫu (xem trước)</Label>
                  <Textarea
                    id="portal-invite-preview"
                    readOnly
                    rows={5}
                    value={portalInviteMessage(client!.name, link)}
                    className="text-xs"
                  />
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" onClick={() => copy(link, 'Đã copy link')}>
                    <Copy aria-hidden="true" />
                    Copy link
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => copy(portalInviteMessage(client!.name, link), 'Đã copy tin nhắn mẫu')}
                  >
                    <MessageSquareText aria-hidden="true" />
                    Copy tin nhắn mẫu
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => window.open(link, '_blank', 'noopener,noreferrer')}>
                    <ExternalLink aria-hidden="true" />
                    Mở thử như khách
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">“Mở thử” mở portal ở tab mới — trình duyệt này sẽ xem portal như khách.</p>
              </div>
            ) : state.status !== 'NONE' ? (
              <p className="text-xs text-muted-foreground">
                Vì lý do bảo mật, link chỉ hiện một lần lúc tạo. Muốn gửi lại cho khách, bấm “Tạo lại”.
              </p>
            ) : null}

            {archived && (
              <p className="text-xs text-muted-foreground">Khách đã lưu trữ nên không thể tạo link mới.</p>
            )}

            {noContact && (
              <p className="flex items-start gap-1.5 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">
                <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                <span>
                  Khách sẽ không thấy cách liên hệ bạn trên portal — thêm số điện thoại hoặc email trong{' '}
                  <Link to="/admin/settings" className="font-medium underline" onClick={() => handleOpenChange(false)}>Cài đặt</Link>.
                </span>
              </p>
            )}

            {confirming && (
              <div className="space-y-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                <p>
                  {confirming === 'regenerate'
                    ? 'Link cũ và mọi phiên khách đang mở sẽ bị vô hiệu ngay. Bạn cần gửi link mới cho khách.'
                    : 'Khách sẽ không mở được portal nữa cho đến khi bạn tạo link mới.'}
                </p>
                <div className="flex justify-end gap-2">
                  <Button size="sm" variant="ghost" onClick={() => setConfirming(null)} disabled={busy}>
                    Huỷ
                  </Button>
                  <Button
                    size="sm"
                    variant={confirming === 'revoke' ? 'destructive' : 'default'}
                    disabled={busy}
                    onClick={() => (confirming === 'regenerate' ? issueMutation.mutate() : revokeMutation.mutate())}
                  >
                    {confirming === 'regenerate' ? 'Tạo link mới' : 'Thu hồi link'}
                  </Button>
                </div>
              </div>
            )}

            {!confirming && (
              <div className="flex flex-wrap justify-end gap-2 pt-1">
                {state.status === 'NONE' ? (
                  <Button onClick={() => issueMutation.mutate()} disabled={busy || archived}>
                    Tạo link
                  </Button>
                ) : (
                  <>
                    <Button variant="outline" onClick={() => setConfirming('revoke')} disabled={busy}>
                      Thu hồi
                    </Button>
                    <Button onClick={() => setConfirming('regenerate')} disabled={busy || archived}>
                      Tạo lại
                    </Button>
                  </>
                )}
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
