import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { FileText } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { PageHeader } from '@/components/PageHeader'
import { StatusBadge } from '@/components/StatusBadge'
import { api } from '@/lib/api'
import { formatDate } from '@/lib/format'
import type { ClientListItem, PagedResult } from '@/lib/types'
import type { EffectiveStatus, PaymentStatus } from '@/lib/quotationTypes'
import { useQuotationSearch } from './useQuotations'

// Radix Select reserves '' for "no value"
const ALL = 'ALL'
const PAGE_SIZE = 20

const statusOptions: { value: EffectiveStatus; label: string }[] = [
  { value: 'DRAFT', label: 'Nháp' },
  { value: 'SENT', label: 'Đã gửi' },
  { value: 'EXPIRED', label: 'Hết hạn' },
  { value: 'ACCEPTED', label: 'Đã chấp nhận' },
  { value: 'REJECTED', label: 'Từ chối' },
  { value: 'SUPERSEDED', label: 'Đã thay thế' },
]

const paymentOptions: { value: PaymentStatus; label: string }[] = [
  { value: 'UNPAID', label: 'Chưa thanh toán' },
  { value: 'DEPOSIT_PAID', label: 'Đã nhận cọc' },
  { value: 'PAID', label: 'Đã thanh toán' },
]

/** docs/08 §2.2 "/admin/quotations": every quotation, filterable by effective status, payment and client. */
export default function QuotationsPage() {
  const [status, setStatus] = useState(ALL)
  const [payment, setPayment] = useState(ALL)
  const [clientId, setClientId] = useState(ALL)
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuotationSearch({
    effectiveStatus: status === ALL ? undefined : status,
    paymentStatus: payment === ALL ? undefined : payment,
    clientId: clientId === ALL ? undefined : clientId,
    page,
    pageSize: PAGE_SIZE,
  })
  const { data: clients } = useQuery({
    queryKey: ['clients', 'options'],
    queryFn: () => api.get<PagedResult<ClientListItem>>('/admin/clients?status=ACTIVE&pageSize=100'),
  })

  const items = data?.items ?? []
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1
  const filter = (set: (v: string) => void) => (value: string) => {
    set(value)
    setPage(1)
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Báo giá" description="Tất cả báo giá của các dự án" />

      <div className="flex flex-wrap gap-3">
        <Select value={status} onValueChange={filter(setStatus)}>
          <SelectTrigger aria-label="Lọc trạng thái" className="w-48"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tất cả trạng thái</SelectItem>
            {statusOptions.map(o => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={payment} onValueChange={filter(setPayment)}>
          <SelectTrigger aria-label="Lọc thanh toán" className="w-48"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Mọi trạng thái thanh toán</SelectItem>
            {paymentOptions.map(o => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={clientId} onValueChange={filter(setClientId)}>
          <SelectTrigger aria-label="Lọc khách hàng" className="w-56"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tất cả khách hàng</SelectItem>
            {(clients?.items ?? []).map(c => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      <div className="rounded-xl border bg-card shadow-card">
        {isLoading ? (
          <div className="space-y-2 p-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : items.length === 0 ? (
          <EmptyState
            icon={FileText}
            title="Không có báo giá"
            description="Không có báo giá nào khớp bộ lọc. Tạo báo giá trong tab Báo giá của từng dự án."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/40 text-left text-xs font-medium text-muted-foreground">
                <tr>
                  <th className="px-4 py-2.5">Số báo giá</th>
                  <th className="px-4 py-2.5">Tiêu đề</th>
                  <th className="px-4 py-2.5">Dự án</th>
                  <th className="px-4 py-2.5">Khách</th>
                  <th className="px-4 py-2.5 text-right">Tổng</th>
                  <th className="px-4 py-2.5">Trạng thái</th>
                  <th className="px-4 py-2.5">Thanh toán</th>
                  <th className="px-4 py-2.5 text-right">Chờ thu</th>
                  <th className="px-4 py-2.5">Hết hạn</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {items.map(q => (
                  <tr key={q.id} className="hover:bg-muted/30">
                    <td className="whitespace-nowrap px-4 py-3 font-medium">
                      <Link to={`/admin/quotations/${q.id}`} className="text-primary hover:underline">
                        {q.number ? `${q.number} · v${q.version}` : 'Bản nháp'}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-foreground">{q.title}</td>
                    <td className="px-4 py-3">
                      <Link to={`/admin/projects/${q.projectId}`} className="text-foreground hover:underline">{q.projectName}</Link>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{q.clientName}</td>
                    <td className="px-4 py-3 text-right"><MoneyText amount={q.total} /></td>
                    <td className="px-4 py-3"><StatusBadge status={q.effectiveStatus} /></td>
                    <td className="px-4 py-3">
                      {q.status === 'ACCEPTED' ? <StatusBadge status={q.paymentStatus} /> : <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="px-4 py-3 text-right"><MoneyText amount={q.outstanding} /></td>
                    <td className="px-4 py-3 tabular-nums text-muted-foreground">{formatDate(q.expiryDate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {pages > 1 && (
          <div className="flex items-center justify-between gap-3 border-t p-4 text-sm text-muted-foreground">
            <span>Trang {page}/{pages}</span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Trang trước</Button>
              <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>Trang sau</Button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
