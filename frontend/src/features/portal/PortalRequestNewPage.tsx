import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { ArrowLeft, CircleCheck, FolderOpen } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { EmptyState } from '@/components/EmptyState'
import { REQUEST_MAX_LENGTH, acceptsRequests, requestErrorMessage, requestSchema, type RequestFormValues } from './requests'
import { usePortalProject, usePortalProjects, useSendPortalRequest } from './usePortal'

// Native <select>: the phone's own picker is the most usable control on mobile (docs/08 POR-05)
const selectClass =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 md:text-sm'

/** docs/08 §3.2 "/portal/requests/new" — text only until M3 adds photos (D2). */
export default function PortalRequestNewPage() {
  const [params] = useSearchParams()
  const presetProjectId = params.get('projectId') ?? ''
  const { data: projects, isLoading } = usePortalProjects()
  const send = useSendPortalRequest()
  const [sent, setSent] = useState(false)

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } = useForm<RequestFormValues>({
    resolver: zodResolver(requestSchema),
    defaultValues: { projectId: presetProjectId, deliverableId: '', content: '' },
  })
  const projectId = watch('projectId')
  const { data: project } = usePortalProject(projectId || undefined)

  // BR-21/53: only open projects are offered — preselect the only one, drop a preselected closed one
  useEffect(() => {
    if (!projects) return
    const ids = projects.filter(p => acceptsRequests(p.status)).map(p => p.id)
    if (projectId && !ids.includes(projectId)) setValue('projectId', '')
    else if (!projectId && ids.length === 1) setValue('projectId', ids[0])
  }, [projects, projectId, setValue])

  const openProjects = (projects ?? []).filter(p => acceptsRequests(p.status))
  const deliverables = (project?.deliverables ?? []).filter(d => d.status !== 'CANCELLED')
  const backTo = presetProjectId ? `/portal/projects/${presetProjectId}` : '/portal/requests'

  function onSubmit(values: RequestFormValues) {
    send.mutate(values, {
      onSuccess: () => {
        reset({ projectId: values.projectId, deliverableId: '', content: '' })
        setSent(true)
      },
      onError: err => toast.error(requestErrorMessage(err)),
    })
  }

  if (sent) {
    return (
      <div className="rounded-xl border bg-card shadow-card">
        <EmptyState
          icon={CircleCheck}
          title="Đã gửi yêu cầu"
          description="Cảm ơn bạn! Designer sẽ xem và phản hồi sớm. Bạn theo dõi trạng thái trong mục Yêu cầu của tôi."
          action={
            <div className="flex flex-wrap justify-center gap-2">
              <Button asChild>
                <Link to="/portal/requests">Xem yêu cầu của tôi</Link>
              </Button>
              <Button variant="outline" onClick={() => setSent(false)}>Gửi yêu cầu khác</Button>
            </div>
          }
        />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <Link to={backTo} className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại
      </Link>
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-foreground">Gửi yêu cầu chỉnh sửa</h1>
        <p className="mt-1 text-sm text-muted-foreground">Designer sẽ xem yêu cầu và báo lại cho bạn.</p>
      </div>

      {isLoading ? (
        <Skeleton className="h-64 w-full rounded-xl" />
      ) : openProjects.length === 0 ? (
        <div className="rounded-xl border bg-card shadow-card">
          <EmptyState
            icon={FolderOpen}
            title="Chưa có dự án nhận yêu cầu"
            description="Dự án đã hoàn thành hoặc đã huỷ không nhận yêu cầu mới. Vui lòng liên hệ trực tiếp designer."
          />
        </div>
      ) : (
        <form
          onSubmit={handleSubmit(onSubmit)}
          noValidate
          className="space-y-5 rounded-xl border bg-card p-4 shadow-card sm:p-5"
        >
          <div className="space-y-2">
            <Label htmlFor="request-project">Dự án</Label>
            <select
              id="request-project"
              className={selectClass}
              aria-invalid={!!errors.projectId}
              aria-describedby={errors.projectId ? 'request-project-error' : undefined}
              {...register('projectId', { onChange: () => setValue('deliverableId', '') })}
            >
              <option value="">Chọn dự án</option>
              {openProjects.map(p => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
            {errors.projectId && (
              <p id="request-project-error" className="text-sm text-destructive">{errors.projectId.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="request-deliverable">Hạng mục (tuỳ chọn)</Label>
            <select id="request-deliverable" className={selectClass} {...register('deliverableId')}>
              <option value="">Cả dự án / chưa rõ hạng mục</option>
              {deliverables.map(d => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="request-content">Nội dung yêu cầu</Label>
            <Textarea
              id="request-content"
              rows={6}
              maxLength={REQUEST_MAX_LENGTH}
              placeholder={'Mỗi ý một dòng, ví dụ:\nĐổi màu chữ sang xanh navy\nTăng cỡ logo thêm 10%'}
              aria-invalid={!!errors.content}
              aria-describedby={errors.content ? 'request-content-hint request-content-error' : 'request-content-hint'}
              {...register('content')}
            />
            <p id="request-content-hint" className="text-xs text-muted-foreground">
              Mỗi ý một dòng giúp designer xử lý nhanh hơn. Đính kèm ảnh sẽ có ở bản cập nhật sau.
            </p>
            {errors.content && (
              <p id="request-content-error" className="text-sm text-destructive">{errors.content.message}</p>
            )}
          </div>

          <Button type="submit" className="w-full" disabled={send.isPending}>
            {send.isPending ? 'Đang gửi…' : 'Gửi yêu cầu'}
          </Button>
        </form>
      )}
    </div>
  )
}
