import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Link2Off, Loader2 } from 'lucide-react'
import { api } from '@/lib/api'
import { PortalMessage } from './PortalMessage'

/** /p/:token — exchanges the magic-link token for a portal session (07-security §4.2). */
export default function PortalTokenPage() {
  const { token = '' } = useParams<{ token: string }>()
  const [failed, setFailed] = useState(false)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return // StrictMode runs effects twice in dev; exchange the token once
    started.current = true
    api.post('/portal/session', { token })
      // Full navigation instead of history.replaceState: it drops the token from the address bar and
      // history AND leaves this page's `Referrer-Policy: no-referrer`, under which the browser sends
      // `Origin: null` on POSTs — CsrfMiddleware would reject every later portal POST.
      .then(() => window.location.replace('/portal'))
      .catch(() => setFailed(true))
  }, [token])

  if (failed) {
    return (
      <PortalMessage
        icon={Link2Off}
        title="Liên kết không hợp lệ hoặc đã hết hạn"
        description="Vui lòng liên hệ designer để nhận link mới."
      />
    )
  }

  return (
    <div role="status" className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 text-sm text-muted-foreground">
      <Loader2 className="h-6 w-6 animate-spin text-primary" aria-hidden="true" />
      Đang mở trang theo dõi dự án…
    </div>
  )
}
