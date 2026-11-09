import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link2, Plus, Search, Users, SearchX } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { EmptyState } from '@/components/EmptyState'
import { MoneyText } from '@/components/MoneyText'
import { Pagination } from '@/components/Pagination'
import { StatusBadge } from '@/components/StatusBadge'
import { tableHeadRowClass, tableRowClass } from '@/components/dataTable'
import { api, emptyToNull } from '@/lib/api'
import type { PagedResult, ClientListItem } from '@/lib/types'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { ClientFields } from './ClientFields'
import { clientSchema, type ClientForm } from './clientForm'
import { PortalLinkDialog } from './PortalLinkDialog'

const PAGE_SIZE = 20

export default function ClientsPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  // ?q= lets other screens link to a filtered list (Plan 07 quick search)
  const [qInput, setQInput] = useState(searchParams.get('q') ?? '')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [sheetOpen, setSheetOpen] = useState(false)
  const [portalClient, setPortalClient] = useState<{ id: string; name: string } | null>(null)
  const q = useDebouncedValue(qInput.trim())

  const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
  if (q) params.set('q', q)
  if (status) params.set('status', status)

  const { data, isLoading } = useQuery({
    queryKey: ['clients', 'list', { q, status, page }],
    queryFn: () => api.get<PagedResult<ClientListItem>>(`/admin/clients?${params}`),
    placeholderData: keepPreviousData,
  })

  const { register, handleSubmit, formState: { errors }, reset } = useForm<ClientForm>({
    resolver: zodResolver(clientSchema),
  })

  const createMutation = useMutation({
    mutationFn: (body: ClientForm) => api.post<{ id: string }>('/admin/clients', emptyToNull(body)),
    onSuccess: created => {
      toast.success('Đã tạo khách hàng')
      qc.invalidateQueries({ queryKey: ['clients'] })
      setSheetOpen(false)
      reset()
      navigate(`/admin/clients/${created.id}`) // QA D-06: straight to the new client's page
    },
    onError: () => toast.error('Không thể tạo khách hàng. Vui lòng thử lại.'),
  })

  const clients = data?.items ?? []
  const filtering = !!(q || status)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Khách hàng"
        description="Danh sách khách và số dự án của từng người."
        actions={
          <Button onClick={() => setSheetOpen(true)}>
            <Plus aria-hidden="true" />
            Thêm khách
          </Button>
        }
      />

      <div className="rounded-xl border bg-card shadow-card">
        <div className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center">
          <div className="relative w-full sm:max-w-xs">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              aria-label="Tìm khách hàng"
              placeholder="Tìm theo tên, công ty, SĐT…"
              value={qInput}
              onChange={e => { setQInput(e.target.value); setPage(1) }}
              className="pl-9"
            />
          </div>
          <Select value={status || '_all'} onValueChange={v => { setStatus(v === '_all' ? '' : v); setPage(1) }}>
            <SelectTrigger className="w-full sm:w-48" aria-label="Lọc theo trạng thái">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="_all">Tất cả trạng thái</SelectItem>
              <SelectItem value="ACTIVE">Đang hoạt động</SelectItem>
              <SelectItem value="INACTIVE">Lưu trữ</SelectItem>
            </SelectContent>
          </Select>
          <span className="text-sm text-muted-foreground sm:ml-auto" aria-live="polite">
            {data?.total ?? 0} khách
          </span>
        </div>

        {isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-11 w-full" />
            ))}
          </div>
        ) : clients.length === 0 ? (
          filtering ? (
            <EmptyState icon={SearchX} title="Không tìm thấy khách phù hợp" description="Thử từ khoá khác hoặc bỏ bộ lọc." />
          ) : (
            <EmptyState
              icon={Users}
              title="Chưa có khách hàng nào"
              description="Thêm khách đầu tiên để bắt đầu tạo dự án."
              action={
                <Button onClick={() => setSheetOpen(true)}>
                  <Plus aria-hidden="true" />
                  Thêm khách
                </Button>
              }
            />
          )
        ) : (
          <div className="relative overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className={tableHeadRowClass}>
                  <th scope="col" className="px-4 py-3">Tên</th>
                  <th scope="col" className="hidden px-4 py-3 md:table-cell">Công ty</th>
                  <th scope="col" className="hidden px-4 py-3 lg:table-cell">SĐT</th>
                  <th scope="col" className="px-4 py-3">Trạng thái</th>
                  <th scope="col" className="px-4 py-3 text-right">Dự án</th>
                  <th scope="col" className="hidden px-4 py-3 text-right lg:table-cell">Chờ thu</th>
                  <th scope="col" className="px-4 py-3 text-right">Portal</th>
                </tr>
              </thead>
              <tbody>
                {clients.map(c => (
                  <tr key={c.id} className={tableRowClass}>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center gap-3">
                        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-secondary font-display text-xs font-bold uppercase text-secondary-foreground" aria-hidden="true">
                          {c.name.slice(0, 1)}
                        </div>
                        <Link
                          to={`/admin/clients/${c.id}`}
                          className="rounded-sm font-semibold text-foreground link-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          {c.name}
                        </Link>
                      </div>
                    </td>
                    <td className="hidden px-4 py-3.5 text-muted-foreground md:table-cell">{c.company ?? '—'}</td>
                    <td className="hidden px-4 py-3.5 tabular-nums text-muted-foreground lg:table-cell">{c.phone ?? '—'}</td>
                    <td className="px-4 py-3.5">
                      <StatusBadge status={c.status} />
                    </td>
                    <td className="px-4 py-3.5 text-right tabular-nums text-muted-foreground">{c.projectCount}</td>
                    <td className="hidden px-4 py-3.5 text-right lg:table-cell">
                      {c.outstanding > 0 ? <MoneyText amount={c.outstanding} /> : <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="px-2 py-2 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={`Link portal của ${c.name}`}
                        onClick={() => setPortalClient({ id: c.id, name: c.name })}
                      >
                        <Link2 aria-hidden="true" />
                        <span className={c.portalEnabled ? 'text-success' : 'text-muted-foreground'}>
                          {c.portalEnabled ? 'Đang bật' : 'Chưa bật'}
                        </span>
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <Pagination page={page} pageSize={PAGE_SIZE} total={data?.total ?? 0} onPageChange={setPage} itemLabel="khách" />
      </div>

      <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-md">
          <SheetHeader>
            <SheetTitle>Thêm khách hàng</SheetTitle>
            <SheetDescription>Chỉ tên là bắt buộc, các thông tin khác có thể bổ sung sau.</SheetDescription>
          </SheetHeader>
          <form onSubmit={handleSubmit(d => createMutation.mutate(d))} className="mt-6 space-y-5" noValidate>
            <ClientFields register={register} errors={errors} />
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>
                Huỷ
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Đang lưu…' : 'Tạo khách'}
              </Button>
            </div>
          </form>
        </SheetContent>
      </Sheet>

      <PortalLinkDialog client={portalClient} onClose={() => setPortalClient(null)} />
    </div>
  )
}
