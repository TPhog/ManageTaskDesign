import { useQuery } from '@tanstack/react-query'
import { Mail, MessageCircle, Phone } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import type { PortalStudio } from '@/lib/portalTypes'
import { qk } from '@/lib/queryKeys'
import { cn } from '@/lib/utils'
import { zaloUrl } from './contact'

interface Props {
  phone: string | null
  email: string | null
  className?: string
}

/** QA C-04/C-13: call / Zalo / email the studio with 44px buttons; renders nothing when Settings has no contact. */
export function StudioContact({ phone, email, className }: Props) {
  if (!phone && !email) return null
  const button = cn(buttonVariants({ variant: 'outline' }), 'h-11 flex-1 sm:flex-none')
  return (
    <div className={cn('flex flex-wrap gap-2', className)}>
      {phone && (
        <a href={`tel:${phone}`} className={button}>
          <Phone aria-hidden="true" />
          Gọi
        </a>
      )}
      {phone && (
        <a href={zaloUrl(phone)} target="_blank" rel="noopener noreferrer" className={button}>
          <MessageCircle aria-hidden="true" />
          Nhắn Zalo
        </a>
      )}
      {email && (
        <a href={`mailto:${email}`} className={button}>
          <Mail aria-hidden="true" />
          Email
        </a>
      )}
    </div>
  )
}

/** QA C-14/C-15, CF-16: the studio's name and contact on pages without a portal session (signed out, expired, bad link) — /api/portal/studio is public. */
export function PublicStudioContact({ className }: { className?: string }) {
  const { data } = useQuery({
    queryKey: qk.portal.studio,
    queryFn: () => api.get<PortalStudio>('/portal/studio'),
    retry: false,
    staleTime: 5 * 60_000,
  })
  if (!data || (!data.contactPhone && !data.contactEmail)) return null
  return (
    <div className={className}>
      {data.displayName && <p className="mb-2 text-sm font-medium text-foreground">Liên hệ {data.displayName}</p>}
      <StudioContact phone={data.contactPhone} email={data.contactEmail} />
    </div>
  )
}
