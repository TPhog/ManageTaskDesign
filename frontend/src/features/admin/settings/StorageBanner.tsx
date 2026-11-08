import { Link, useLocation } from 'react-router-dom'
import { TriangleAlert } from 'lucide-react'
import { cn } from '@/lib/utils'
import { storageBanner } from './storage'
import { useStorageUsage } from './useStorage'

/** Admin-wide warning: yellow ≥ 85 %, red ≥ 90 % (spec §2). Hidden on the storage page itself. */
export function StorageBanner() {
  const { data } = useStorageUsage()
  const { pathname } = useLocation()
  const banner = storageBanner(data)
  if (!banner || pathname.startsWith('/admin/settings/storage')) return null
  return (
    <div
      role="status"
      className={cn(
        'mb-6 flex flex-wrap items-center gap-2 rounded-lg border px-4 py-3 text-sm',
        banner.tone === 'warning' ? 'border-amber-200 bg-amber-50 text-amber-900' : 'border-red-200 bg-red-50 text-red-900',
      )}
    >
      <TriangleAlert className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span className="flex-1">{banner.text}</span>
      <Link to="/admin/settings/storage" className="font-medium underline underline-offset-2">Mở Dung lượng</Link>
    </div>
  )
}
