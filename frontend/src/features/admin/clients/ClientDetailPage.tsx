import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { ChevronRight, Link2, Pencil, SearchX } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { api, emptyToNull, type ApiError } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { qk } from '@/lib/queryKeys'
import type { ClientDetail, PagedResult, ProjectListItem } from '@/lib/types'
import { useQuotationSearch } from '@/features/admin/quotations/useQuotations'
import { ClientFields } from './ClientFields'
import { clientSchema, type ClientForm } from './clientForm'
import { CreateProjectDialog } from '@/features/admin/projects/CreateProjectDialog'
import { PortalLinkDialog } from './PortalLinkDialog'

const panelClass = 'rounded-xl border bg-card p-5 shadow-card'

function Info({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm font-medium text-foreground">{value || '—'}</dd>
    </div>
  )
}

function Stat({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border bg-card p-4 shadow-card">
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-lg font-semibold tabular-nums text-foreground">{children}</dd>
    </div>
  )
}

/** QA D-06: one place for a client's contact, money, projects, quotations and portal link. */
export default function ClientDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const [editOpen, setEditOpen] = useState(false)
  const [portalOpen, setPortalOpen] = useState(false)
  const [projectOpen, setProjectOpen] = useState(false)

  const { data: client, isLoading, error } = useQuery({
    queryKey: qk.clients.detail(id),
    queryFn: () => api.get<ClientDetail>(`/admin/clients/${id}`),
    enabled: !!id,
  })
  const { data: projects } = useQuery({
    queryKey: [...qk.projects.all, 'by-client', id],
    queryFn: () => api.get<PagedResult<ProjectListItem>>(`/admin/projects?clientId=${id}&sort=createdAt:desc&pageSize=100`),
    enabled: !!id,
  })
  const { data: quotations } = useQuotationSearch({ clientId: id, pageSize: 20 })

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-5 w-48" />
        <Skeleton className="h-36 w-full rounded-xl" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    )
  }

  if (error || !client) {
    return (
      <div className={panelClass}>
        <EmptyState
          icon={SearchX}
          title="Không tìm thấy khách hàng"
          description="Khách có thể đã bị xoá hoặc đường dẫn không đúng."
          action={
            <Button asChild variant="outline">
              <Link to="/admin/clients">Về danh sách khách</Link>
            </Button>
          }
        />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/admin/clients" className="rounded-sm hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          Khách hàng
        </Link>
        <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
        <span className="truncate font-medium text-foreground" aria-current="page">{client.name}</span>
      </nav>

      <div className={panelClass}>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="truncate text-2xl font-semibold tracking-tight text-foreground">{client.name}</h1>
              <StatusBadge status={client.status} />
            </div>
            {client.company && <p className="mt-1 text-sm text-muted-foreground">{client.company}</p>}
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setPortalOpen(true)}>
              <Link2 aria-hidden="true" />
              Link portal
            </Button>
            <Button variant="outline" onClick={() => setEditOpen(true)} disabled={client.status === 'INACTIVE'}>
              <Pencil aria-hidden="true" />
              Sửa thông tin
            </Button>
          </div>
        </div>
        <dl className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-3">
          <Info label="Số điện thoại" value={client.phone} />
          <Info label="Email" value={client.email} />
          <Info label="Kênh liên hệ" value={client.contactChannel} />
        </dl>
        {client.notes && (
          <div className="mt-5">
            <p className="mb-1.5 text-xs font-medium text-muted-foreground">Ghi chú nội bộ (khách không thấy)</p>
            <p className="whitespace-pre-wrap rounded-lg bg-muted p-3 text-sm text-foreground">{client.notes}</p>
          </div>
        )}
      </div>

      <dl className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <Stat label="Dự án">{client.projectCount}</Stat>
        <Stat label="Đã chốt"><MoneyText amount={client.acceptedTotal} /></Stat>
        <Stat label="Chờ thu"><MoneyText amount={client.outstanding} /></Stat>
        <Stat label="Báo giá chờ phản hồi"><MoneyText amount={client.pendingQuotationTotal} /></Stat>
        <Stat label="Yêu cầu mới">
          {client.newRequests > 0 ? <Link to="/admin/requests" className="text-primary hover:underline">{client.newRequests}</Link> : 0}
        </Stat>
      </dl>

      <section aria-labelledby="client-projects" className="rounded-xl border bg-card shadow-card">
        <h2 id="client-projects" className="border-b p-4 text-sm font-semibold text-foreground">Dự án</h2>
        {!projects ? (
          <Skeleton className="m-4 h-16" />
        ) : projects.items.length === 0 ? (
          <div className="flex flex-wrap items-center justify-between gap-3 p-4">
            <p className="text-sm text-muted-foreground">Chưa có dự án nào.</p>
            <Button size="sm" onClick={() => setProjectOpen(true)}>Tạo dự án cho khách này</Button>
          </div>
        ) : (
          <ul className="divide-y">
            {projects.items.map(p => (
              <li key={p.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <Link to={`/admin/projects/${p.id}`} className="min-w-0 truncate text-sm font-medium text-foreground hover:text-primary hover:underline">
                  {p.name}
                </Link>
                <div className="flex items-center gap-3 text-xs text-muted-foreground">
                  <span className="tabular-nums">Hạn {formatDate(p.dueDate)}</span>
                  <StatusBadge status={p.status} />
                </div>
              </li>
            ))}
          </ul>
        )}
        {projects && projects.total > projects.items.length && (
          <div className="border-t p-4 text-sm">
            <Link to={`/admin/projects?clientId=${id}`} className="font-medium text-primary hover:underline">
              Xem tất cả {projects.total} dự án
            </Link>
          </div>
        )}
      </section>

      <section aria-labelledby="client-quotations" className="rounded-xl border bg-card shadow-card">
        <h2 id="client-quotations" className="border-b p-4 text-sm font-semibold text-foreground">Báo giá</h2>
        {!quotations ? (
          <Skeleton className="m-4 h-16" />
        ) : quotations.items.length === 0 ? (
          <p className="p-4 text-sm text-muted-foreground">Chưa có báo giá nào.</p>
        ) : (
          <ul className="divide-y">
            {quotations.items.map(q => (
              <li key={q.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <Link to={`/admin/quotations/${q.id}`} className="min-w-0 truncate text-sm font-medium text-foreground hover:text-primary hover:underline">
                  {q.number ? `${q.number} · v${q.version}` : 'Bản nháp'} — {q.title}
                </Link>
                <div className="flex items-center gap-3 text-xs">
                  <MoneyText amount={q.total} />
                  <StatusBadge status={q.effectiveStatus} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <ClientEditSheet client={client} open={editOpen} onClose={() => setEditOpen(false)} />
      <CreateProjectDialog
        key={client.id}
        open={projectOpen}
        onClose={() => setProjectOpen(false)}
        defaultClientId={client.id}
        defaultClientLabel={client.name}
      />
      <PortalLinkDialog client={portalOpen ? { id: client.id, name: client.name } : null} onClose={() => setPortalOpen(false)} />
    </div>
  )
}

function ClientEditSheet({ client, open, onClose }: { client: ClientDetail; open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<ClientForm>({
    resolver: zodResolver(clientSchema),
    values: {
      name: client.name,
      company: client.company ?? '',
      phone: client.phone ?? '',
      email: client.email ?? '',
      contactChannel: client.contactChannel ?? '',
      notes: client.notes,
    },
  })

  const mutation = useMutation({
    mutationFn: (v: ClientForm) => api.put(`/admin/clients/${client.id}`, emptyToNull(v)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clients'] })
      qc.invalidateQueries({ queryKey: ['projects'] })
      toast.success('Đã cập nhật khách hàng')
      onClose()
    },
    onError: (err: ApiError) => toast.error(err.detail ?? 'Không thể cập nhật khách hàng'),
  })

  return (
    <Sheet open={open} onOpenChange={o => { if (!o) onClose() }}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-md">
        <SheetHeader>
          <SheetTitle>Sửa thông tin khách</SheetTitle>
          <SheetDescription>Thay đổi áp dụng cho các báo giá gửi sau này; báo giá đã gửi giữ nguyên.</SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(v => mutation.mutate(v))} className="mt-6 space-y-5" noValidate>
          <ClientFields register={register} errors={errors} idPrefix="edit-" />
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Huỷ</Button>
            <Button type="submit" disabled={mutation.isPending}>{mutation.isPending ? 'Đang lưu…' : 'Lưu'}</Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  )
}
