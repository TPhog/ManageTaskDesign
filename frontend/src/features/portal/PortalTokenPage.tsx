import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Link2Off, Loader2 } from 'lucide-react'
import { api } from '@/lib/api'
import { PortalMessage } from './PortalMessage'
import { PublicStudioContact } from './StudioContact'
import { sessionFailureKind, type PortalSessionFailure } from './errors'
import { usePortalTheme } from './kit/usePortalTheme'

/** /p/:token — exchanges the magic-link token for a portal session (07-security §4.2). */
export default function PortalTokenPage() {
  usePortalTheme()
  const { token = '' } = useParams<{ token: string }>()
  const [failed, setFailed] = useState<PortalSessionFailure | null>(token ? null : 'INVALID')
  const started = useRef(false)

  useEffect(() => {
    if (!token || started.current) return // StrictMode runs effects twice in dev; exchange the token once
    started.current = true
    api.post('/portal/session', { token })
      // Full navigation instead of history.replaceState: it drops the token from the address bar and
      // history AND leaves this page's `Referrer-Policy: no-referrer`, under which the browser sends
      // `Origin: null` on POSTs — CsrfMiddleware would reject every later portal POST.
      .then(() => window.location.replace('/portal'))
      .catch(err => {
        // Drop the token from the address bar/history (07-security §4.2) — no navigation, the error stays on screen.
        window.history.replaceState(null, '', '/p')
        setFailed(sessionFailureKind(err))
      })
  }, [token])

  if (failed === 'RATE_LIMITED') {
    return (
      <PortalMessage
        icon={Link2Off}
        title="Bạn thử lại sau 1 phút nhé"
        description="Có quá nhiều lần thử mở link. Chờ một lát rồi mở lại link nhà thiết kế đã gửi."
        action={<PublicStudioContact />}
      />
    )
  }

  if (failed) {
    return (
      <PortalMessage
        icon={Link2Off}
        title="Liên kết không hợp lệ hoặc đã hết hạn"
        description="Vui lòng liên hệ nhà thiết kế để nhận link mới."
        action={<PublicStudioContact />}
      />
    )
  }

  return (
    <div role="status" className="flex min-h-screen flex-col items-center justify-center gap-3 bg-background text-sm text-muted-foreground">
      <Loader2 className="h-6 w-6 animate-spin text-highlight-text" aria-hidden="true" />
      Đang mở trang theo dõi dự án…
    </div>
  )
}
