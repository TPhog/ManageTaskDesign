import { useEffect, useMemo, useRef, useState, type ComponentProps, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { CircleCheck, Link2Off, Mail, Phone, WifiOff, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { EmptyState } from '@/components/EmptyState'
import { ImagePicker } from '@/components/ImagePicker'
import { TemplateFields } from '@/components/TemplateFields'
import { todayVn } from '@/features/admin/quotations/quotationUtils'
import { PortalMessage } from '@/features/portal/PortalMessage'
import { RequestKindPicker } from '@/features/portal/RequestKindPicker'
import { EditorialSection, Kicker } from '@/features/portal/kit/editorial'
import { usePortalTheme } from '@/features/portal/kit/usePortalTheme'
import { splitWordmark } from '@/features/portal/kit/wordmark'
import type { ApiError } from '@/lib/api'
import { isStorageFull } from '@/lib/images'
import {
  FORM_ERROR_KEY, NOTHING_ANSWERED, TEMPLATE_LIMITS, answerErrorsFromApi, emptyDrafts, nothingAnswered, toAnswersPayload, validateDrafts,
  type AnswerDrafts,
} from '@/lib/requestTemplates'
import { contactSchema, intakeErrorMessage, isFormExpired, toIntakeForm, type IntakeContact } from './intake'
import { usePublicIntakeForm, useSubmitIntake } from './usePublicIntake'

/** The server drops a post sent sooner than PublicIntake:MinFillSeconds (3 s) after the form token: never send that early. */
const MIN_FILL_MS = 3500
const CONTACT_FIELDS = ['contactName', 'contactPhone', 'contactEmail'] as const
const emptyContact: IntakeContact = { contactName: '', contactPhone: '', contactEmail: '' }
const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms))

/** Plan 13 "/f/:slug": the public form for new clients. No session: only GET/POST /api/public/intake/{slug}. */
export default function PublicIntakePage() {
  usePortalTheme()
  const { slug = '' } = useParams()
  const { data: form, error, refetch, isFetching, dataUpdatedAt } = usePublicIntakeForm(slug)
  const submit = useSubmitIntake(slug)

  const [templateId, setTemplateId] = useState('')
  const [drafts, setDrafts] = useState<AnswerDrafts>({})
  const [answerErrors, setAnswerErrors] = useState<Record<string, string>>({})
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState<string | null>(null)
  const [files, setFiles] = useState<File[]>([])
  const [filesError, setFilesError] = useState<string | null>(null)
  const [photosRefused, setPhotosRefused] = useState(false)
  const [honeypot, setHoneypot] = useState('')
  const [waiting, setWaiting] = useState(false)
  const [sent, setSent] = useState<{ name: string; phone: string } | null>(null)
  const { register, trigger, getValues, getFieldState, setError, reset, formState: { errors } } = useForm<IntakeContact>({
    resolver: zodResolver(contactSchema),
    defaultValues: emptyContact,
  })

  const previews = useMemo(() => files.map(f => URL.createObjectURL(f)), [files])
  useEffect(() => () => previews.forEach(u => URL.revokeObjectURL(u)), [previews])

  if (!form) {
    if ((error as Partial<ApiError> | null)?.status === 404) {
      return <PortalMessage icon={Link2Off} title="Link không còn hoạt động" description="Vui lòng liên hệ trực tiếp studio để được hỗ trợ." />
    }
    if (error) {
      return (
        <PortalMessage
          icon={WifiOff}
          title="Không tải được biểu mẫu"
          description="Vui lòng kiểm tra kết nối mạng rồi thử lại."
          action={<Button size="touch" onClick={() => void refetch()}>Thử lại</Button>}
        />
      )
    }
    return (
      <div className="min-h-dvh bg-background">
        <div className="mx-auto max-w-xl space-y-4 px-4 py-6 sm:py-10" role="status" aria-label="Đang tải biểu mẫu">
          <Skeleton className="mx-auto h-9 w-2/3" />
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-32 w-full" />)}
        </div>
      </div>
    )
  }

  const template = form.templates.find(t => t.id === templateId)
  // storage nearly full (maxImages 0): no photo question, so no "attach at least one photo" check either
  const fields = template ? template.fields.filter(f => form.maxImages > 0 || f.type !== 'IMAGES') : []
  const busy = waiting || submit.isPending || isFetching
  const studio = form.studio
  const { head, tail } = splitWordmark(studio.displayName)

  function pickTemplate(id: string) {
    setTemplateId(id)
    setDrafts(emptyDrafts(form?.templates.find(t => t.id === id)?.fields ?? []))
    setAnswerErrors({})
    setFiles([])
    setFilesError(null)
  }

  async function send(filesToSend: File[]) {
    const contactOk = await trigger()
    const answerErrs = template ? validateDrafts(fields, drafts, filesToSend.length, todayVn()) : {}
    const noteErr = !template && note.trim().length === 0 ? 'Mô tả yêu cầu của bạn' : null
    const firstAnswer = fields.find(f => answerErrs[f.key])?.key
    // the server refuses a template request with nothing in it: say so before spending one of the hourly sends
    if (template && !firstAnswer && nothingAnswered(fields, drafts, filesToSend.length, note)) answerErrs[FORM_ERROR_KEY] = NOTHING_ANSWERED
    setAnswerErrors(answerErrs)
    setNoteError(noteErr)
    const firstContact = CONTACT_FIELDS.find(f => getFieldState(f).invalid)
    if (firstAnswer || noteErr || answerErrs[FORM_ERROR_KEY] || !contactOk) {
      const target = firstAnswer ? `intake-${firstAnswer}` : noteErr || answerErrs[FORM_ERROR_KEY] ? 'intake-note' : `intake-${firstContact}`
      const el = document.getElementById(target)
      if (el) el.focus()
      if (answerErrs[FORM_ERROR_KEY]) toast.error(answerErrs[FORM_ERROR_KEY])
      else if (!el) toast.error('Còn thông tin chưa điền — vui lòng kiểm tra lại.')
      return
    }
    if (!form) return
    const contact = getValues()
    setWaiting(true)
    try {
      const early = dataUpdatedAt + MIN_FILL_MS - Date.now()
      if (early > 0) await sleep(early)
      await submit.mutateAsync(toIntakeForm({
        formToken: form.formToken, honeypot, contact, templateId: template?.id ?? '',
        answers: template ? toAnswersPayload(fields, drafts) : undefined, note, files: filesToSend,
      }))
      setSent({ name: contact.contactName.trim(), phone: contact.contactPhone.trim() })
    } catch (err) {
      const e = err as Partial<ApiError>
      if (isFormExpired(err)) {
        await refetch()   // a fresh single-use token; everything typed stays
      } else if (e.errors?.templateId) {
        // the studio hid or deleted the chosen template meanwhile: reload the list in place, keep contact and note
        const fresh = await refetch()
        if (!fresh.data?.templates.some(t => t.id === templateId)) setTemplateId('')
      } else {
        if (isStorageFull(err)) setPhotosRefused(true)
        setAnswerErrors(answerErrorsFromApi(e.errors))
        setNoteError(e.errors?.note?.[0] ?? null)
        setFilesError(e.errors?.files?.[0] ?? null)
        for (const field of CONTACT_FIELDS) {
          const message = e.errors?.[field]?.[0]
          if (message) setError(field, { message })
        }
      }
      toast.error(intakeErrorMessage(err))
    } finally {
      setWaiting(false)
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    void send(files)
  }

  function sendWithoutPhotos() {
    setFiles([])
    setPhotosRefused(false)
    void send([])
  }

  function startOver() {
    setSent(null)
    setTemplateId('')
    setDrafts({})
    setAnswerErrors({})
    setNote('')
    setNoteError(null)
    setFiles([])
    setFilesError(null)
    setPhotosRefused(false)
    reset(emptyContact)
    void refetch()   // the token was spent by the sent form
  }

  const sections = form.templates.length > 0
    ? { kind: 'I.', details: 'II.', contact: 'III.' }
    : { kind: '', details: 'I.', contact: 'II.' }

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-xl px-4 py-6 sm:py-10">
        <header className="text-center">
          <Kicker tone="highlight">Yêu cầu thiết kế</Kicker>
          <h1 className="mt-2 font-display text-[1.75rem] font-medium leading-[1.15] tracking-[-0.01em] text-foreground sm:text-[2rem]">
            {head}{tail && <i className="text-highlight-text">{tail}</i>}
          </h1>
          <div className="mt-3.5 h-1.5 border-t-[3px] border-b border-rule" aria-hidden="true" />
          <p className="mt-3 font-serif text-base text-muted-foreground">Gửi yêu cầu thiết kế — studio sẽ liên hệ lại bạn.</p>
          {(studio.contactPhone || studio.contactEmail) && (
            <div className="mt-2 flex flex-wrap justify-center gap-x-4">
              {studio.contactPhone && (
                <a href={`tel:${studio.contactPhone.replace(/[^\d+]/g, '')}`} className="link-inline inline-flex min-h-11 items-center gap-1.5 text-sm">
                  <Phone className="h-3.5 w-3.5" aria-hidden="true" />
                  {studio.contactPhone}
                </a>
              )}
              {studio.contactEmail && (
                <a href={`mailto:${studio.contactEmail}`} className="link-inline inline-flex min-h-11 min-w-0 items-center gap-1.5 break-all text-sm">
                  <Mail className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                  {studio.contactEmail}
                </a>
              )}
            </div>
          )}
        </header>

        {sent ? (
          <SentCard
            text={`Cảm ơn ${sent.name}! ${studio.displayName} sẽ liên hệ bạn qua số ${sent.phone} sớm nhất.`}
            onAgain={startOver}
          />
        ) : (
          <form onSubmit={onSubmit} noValidate className="relative mt-8">
            <fieldset disabled={busy} className="m-0 min-w-0 border-0 p-0">
              {form.templates.length > 0 && (
                <EditorialSection number={sections.kind} title="Bạn cần thiết kế gì?">
                  <div className="pt-3">
                    <RequestKindPicker templates={form.templates} value={templateId} onChange={pickTemplate} freeTextLabel="Yêu cầu khác (tự mô tả)" />
                  </div>
                </EditorialSection>
              )}

              <EditorialSection number={sections.details} title={template ? template.name : 'Mô tả yêu cầu'}>
                <div className="space-y-5 pt-3">
                  {template ? (
                    <>
                      <TemplateFields
                        idPrefix="intake"
                        fields={fields}
                        drafts={drafts}
                        onChange={(key, draft) => setDrafts(current => ({ ...current, [key]: draft }))}
                        errors={answerErrors}
                        files={files}
                        onFilesChange={setFiles}
                        maxImages={form.maxImages}
                        maxImageMb={form.maxImageMb}
                      />
                      <div className="space-y-2">
                        <Label htmlFor="intake-note">Ghi chú thêm (tuỳ chọn)</Label>
                        <Textarea id="intake-note" rows={3} maxLength={TEMPLATE_LIMITS.maxNote} value={note}
                          aria-invalid={!!noteError} aria-describedby={noteError ? 'intake-note-error' : undefined}
                          onChange={e => setNote(e.target.value)} />
                        {noteError && <p id="intake-note-error" className="text-sm text-destructive">{noteError}</p>}
                      </div>
                    </>
                  ) : (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="intake-note">Mô tả yêu cầu của bạn <span className="text-destructive" aria-hidden="true">*</span></Label>
                        <Textarea id="intake-note" rows={6} maxLength={TEMPLATE_LIMITS.maxNote} value={note} aria-required
                          placeholder="Bạn cần thiết kế gì, dùng ở đâu, khi nào cần…"
                          aria-invalid={!!noteError} aria-describedby={noteError ? 'intake-note-error' : undefined}
                          onChange={e => setNote(e.target.value)} />
                        {noteError && <p id="intake-note-error" className="text-sm text-destructive">{noteError}</p>}
                      </div>
                      {form.maxImages > 0 && (
                        <div className="space-y-2">
                          <Label htmlFor="intake-images">Ảnh tham khảo (tuỳ chọn)</Label>
                          <ImagePicker
                            id="intake-images"
                            max={Math.max(1, form.maxImages - files.length)}
                            disabled={files.length >= form.maxImages}
                            onFiles={picked => setFiles(current => [...current, ...picked].slice(0, form.maxImages))}
                            label="Thêm ảnh"
                            hint={`Tối đa ${form.maxImages} ảnh, ${form.maxImageMb} MB/ảnh — JPG, PNG, WebP`}
                          />
                          {files.length > 0 && (
                            <ul className="grid grid-cols-3 gap-2" aria-label="Ảnh đính kèm">
                              {files.map((f, i) => (
                                <li key={`${f.name}-${i}`} className="relative aspect-square overflow-hidden border border-border bg-muted">
                                  <img src={previews[i]} alt={f.name} className="h-full w-full object-cover" />
                                  <button
                                    type="button"
                                    aria-label={`Bỏ ảnh ${f.name}`}
                                    onClick={() => setFiles(files.filter((_, j) => j !== i))}
                                    className="absolute right-0 top-0 flex h-11 w-11 items-center justify-center bg-surface-inverse/75 text-on-inverse transition-colors hover:bg-surface-inverse focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-on-inverse-accent"
                                  >
                                    <X className="h-4 w-4" aria-hidden="true" />
                                  </button>
                                </li>
                              ))}
                            </ul>
                          )}
                        </div>
                      )}
                    </>
                  )}
                  {filesError && <p className="text-sm text-destructive">{filesError}</p>}
                </div>
              </EditorialSection>

              <EditorialSection number={sections.contact} title="Thông tin liên hệ">
                <div className="space-y-4 pt-3">
                  <ContactInput id="intake-contactName" label="Tên của bạn" required error={errors.contactName?.message}
                    autoComplete="name" {...register('contactName')} />
                  <ContactInput id="intake-contactPhone" label="Số điện thoại / Zalo" required error={errors.contactPhone?.message}
                    type="tel" inputMode="tel" autoComplete="tel" {...register('contactPhone')} />
                  <ContactInput id="intake-contactEmail" label="Email (tuỳ chọn)" error={errors.contactEmail?.message}
                    type="email" autoComplete="email" {...register('contactEmail')} />
                </div>
              </EditorialSection>

              {/* BR-138 honeypot: invisible to people and screen readers; bots that fill it get a silent 204. It is sent as
                  "website", but the input has an unusual name and opts out of autofill and password managers, so a real
                  visitor's browser never fills it and loses the request behind a fake success screen. */}
              <div aria-hidden="true" className="absolute -left-[10000px] h-px w-px overflow-hidden">
                <label>
                  Để trống ô này
                  <input name="ff_hp_confirm_url" tabIndex={-1} autoComplete="off" data-1p-ignore="true" data-lpignore="true"
                    data-form-type="other" value={honeypot} onChange={e => setHoneypot(e.target.value)} />
                </label>
              </div>

              <div className="mt-8 space-y-3 border-t-[3px] border-rule pt-5">
                {answerErrors[FORM_ERROR_KEY] && <p className="text-sm text-destructive">{answerErrors[FORM_ERROR_KEY]}</p>}
                {photosRefused && files.length > 0 && (
                  <div role="alert" className="space-y-2 border-l-4 border-warning py-1 pl-4 text-sm text-foreground">
                    <p>Hiện studio chưa nhận thêm ảnh — bạn có thể gửi yêu cầu không kèm ảnh, rồi gửi ảnh qua Zalo sau.</p>
                    <Button type="button" variant="outline" className="h-11" onClick={sendWithoutPhotos}>Gửi không kèm ảnh</Button>
                  </div>
                )}
                <Button type="submit" size="touch" className="w-full">
                  {waiting || submit.isPending ? 'Đang gửi…' : 'Gửi yêu cầu'}
                </Button>
                <p className="text-center text-xs text-muted-foreground">Thông tin chỉ dùng để studio liên hệ lại với bạn.</p>
              </div>
            </fieldset>
          </form>
        )}
      </div>
    </div>
  )
}

type ContactInputProps = ComponentProps<typeof Input> & { id: string; label: string; required?: boolean; error?: string }

function ContactInput({ id, label, required, error, ...input }: ContactInputProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive" aria-hidden="true"> *</span>}
      </Label>
      <Input id={id} className="h-11" aria-required={required} aria-invalid={!!error}
        aria-describedby={error ? `${id}-error` : undefined} {...input} />
      {error && <p id={`${id}-error`} className="text-sm text-destructive">{error}</p>}
    </div>
  )
}

/** Replaces the form; focus moves here so screen readers read the thank-you (a pre-filled live region stays silent). */
function SentCard({ text, onAgain }: { text: string; onAgain: () => void }) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => ref.current?.focus(), [])
  return (
    <div ref={ref} tabIndex={-1} className="mt-8 border-t-[3px] border-rule focus:outline-none">
      <EmptyState
        icon={CircleCheck}
        title="Đã gửi yêu cầu"
        description={text}
        action={<Button type="button" variant="outline" size="touch" onClick={onAgain}>Gửi yêu cầu khác</Button>}
      />
    </div>
  )
}
