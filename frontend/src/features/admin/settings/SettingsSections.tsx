import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Check, ChevronsUpDown } from 'lucide-react'
import { toast } from 'sonner'
import { api, type ApiError } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { cn } from '@/lib/utils'
import { useRequestTemplates } from '@/features/admin/request-templates/useRequestTemplates'
import { DEFAULT_TERMS_TEMPLATE } from './termsTemplate'
import { applyServerErrors, useReportDirty, useSettingsSection, type Settings } from './settingsForm'
import { Field, SaveBar, SettingsSection } from './SettingsSection'

interface Bank { bin: string; code: string; shortName: string; name: string }

const QR_IMAGE_CLASS = 'h-40 w-40 rounded-md border bg-white' // palette-ok: a QR code must stay dark on white to scan

function a11y(id: string, error?: unknown) {
  return { 'aria-invalid': !!error, 'aria-describedby': error ? `${id}-error` : undefined }
}

function SaveButton({ canSave, saving, label }: { canSave: boolean; saving: boolean; label: string }) {
  return (
    <Button type="submit" disabled={!canSave} className="min-w-36">
      {saving ? 'Đang lưu…' : label}
    </Button>
  )
}

// ── Studio ─────────────────────────────────────────────────────────────────────

const studioSchema = z.object({
  displayName: z.string().min(1, 'Bắt buộc'),
  contactEmail: z.string().email('Email không hợp lệ').or(z.literal('')),
  contactPhone: z.string(),
})

export function StudioSection({ settings }: { settings: Settings }) {
  const { form, onSubmit, saving, canSave } = useSettingsSection({
    dirtyKey: 'studio',
    settings,
    schema: studioSchema,
    pick: s => ({ displayName: s.displayName, contactEmail: s.contactEmail ?? '', contactPhone: s.contactPhone ?? '' }),
    savedMessage: 'Đã lưu hồ sơ',
  })
  const { register, formState: { errors } } = form

  return (
    <SettingsSection id="settings-studio" title="Hồ sơ" description="Thông tin hiển thị trên báo giá và portal gửi khách.">
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <Field id="settings-displayName" label="Tên hiển thị" error={errors.displayName?.message}>
          <Input id="settings-displayName" {...a11y('settings-displayName', errors.displayName)} {...register('displayName')} />
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field id="settings-contactEmail" label="Email liên hệ" error={errors.contactEmail?.message}>
            <Input id="settings-contactEmail" type="email" {...a11y('settings-contactEmail', errors.contactEmail)} {...register('contactEmail')} />
          </Field>
          <Field
            id="settings-contactPhone"
            label="Số điện thoại"
            hint="Khách thấy số này (nút Gọi / Nhắn Zalo) và email trên portal và báo giá — nên điền ít nhất một."
            error={errors.contactPhone?.message}
          >
            <Input id="settings-contactPhone" type="tel" inputMode="tel" {...a11y('settings-contactPhone', errors.contactPhone)} {...register('contactPhone')} />
          </Field>
        </div>
        <SaveBar><SaveButton canSave={canSave} saving={saving} label="Lưu hồ sơ" /></SaveBar>
      </form>
    </SettingsSection>
  )
}

// ── Thanh toán ─────────────────────────────────────────────────────────────────

const paymentSchema = z.object({
  bankBin: z.string(),
  bankAccountNo: z.string().regex(/^[A-Za-z0-9]{4,19}$/, '4–19 ký tự chữ hoặc số').or(z.literal('')),
  bankAccountName: z.string(),
})

export function PaymentSection({ settings }: { settings: Settings }) {
  const [bankOpen, setBankOpen] = useState(false)
  const [showQr, setShowQr] = useState(false)
  const { data: banks } = useQuery({ queryKey: ['banks'], queryFn: () => api.get<Bank[]>('/admin/banks') })
  const { form, onSubmit, saving, canSave } = useSettingsSection({
    dirtyKey: 'payment',
    settings,
    schema: paymentSchema,
    pick: s => ({ bankBin: s.bankBin ?? '', bankAccountNo: s.bankAccountNo ?? '', bankAccountName: s.bankAccountName ?? '' }),
    savedMessage: 'Đã lưu tài khoản ngân hàng',
  })
  const { register, watch, setValue, formState: { errors } } = form
  const selectedBin = watch('bankBin')
  const selectedBank = banks?.find(b => b.bin === selectedBin)
  // the test QR is drawn from the SAVED account, so offer it only once one is saved
  const savedAccount = !!settings.bankBin && !!settings.bankAccountNo

  return (
    <SettingsSection id="settings-payment" title="Ngân hàng" description="Dùng để tạo mã VietQR trên báo giá. Có thể để trống.">
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <Field id="settings-bank" label="Ngân hàng" error={errors.bankBin?.message}>
          <Popover open={bankOpen} onOpenChange={setBankOpen}>
            <PopoverTrigger asChild>
              <Button
                id="settings-bank"
                type="button"
                variant="outline"
                role="combobox"
                aria-expanded={bankOpen}
                className="w-full justify-between font-normal"
              >
                <span className={cn('truncate', !selectedBank && 'text-muted-foreground')}>
                  {selectedBank ? `${selectedBank.shortName} — ${selectedBank.name}` : 'Chọn ngân hàng…'}
                </span>
                <ChevronsUpDown className="opacity-50" aria-hidden="true" />
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
              <Command>
                <CommandInput placeholder="Tìm theo tên hoặc BIN…" />
                <CommandList>
                  <CommandEmpty>Không tìm thấy ngân hàng.</CommandEmpty>
                  <CommandGroup>
                    {banks?.map(bank => (
                      <CommandItem
                        key={bank.bin}
                        value={`${bank.shortName} ${bank.name} ${bank.bin}`}
                        onSelect={() => {
                          setValue('bankBin', bank.bin, { shouldDirty: true })
                          setBankOpen(false)
                        }}
                      >
                        <Check className={cn('mr-2 h-4 w-4', selectedBin === bank.bin ? 'opacity-100' : 'opacity-0')} />
                        {bank.shortName} — {bank.name}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field id="settings-bankAccountNo" label="Số tài khoản" error={errors.bankAccountNo?.message}>
            <Input id="settings-bankAccountNo" inputMode="numeric" {...a11y('settings-bankAccountNo', errors.bankAccountNo)} {...register('bankAccountNo')} />
          </Field>
          <Field id="settings-bankAccountName" label="Tên chủ tài khoản" hint="Viết hoa, không dấu" error={errors.bankAccountName?.message}>
            <Input
              id="settings-bankAccountName"
              placeholder="NGUYEN VAN A"
              className="uppercase"
              {...a11y('settings-bankAccountName', errors.bankAccountName)}
              {...register('bankAccountName')}
            />
          </Field>
        </div>
        {savedAccount && (
          <div className="space-y-3">
            <Button type="button" variant="outline" size="sm" onClick={() => setShowQr(v => !v)}>
              {showQr ? 'Ẩn QR' : 'Thử QR 10.000 ₫'}
            </Button>
            {showQr && (
              <div className="flex flex-wrap items-center gap-4 rounded-lg border border-border bg-surface-sunk p-4">
                <img src={`/api/admin/settings/test-qr.png?v=${settings.rowVersion}`} alt="Mã QR thử 10.000 ₫" className={QR_IMAGE_CLASS} />
                <p className="min-w-0 flex-1 basis-48 text-sm text-muted-foreground">Quét bằng app ngân hàng để kiểm tra tài khoản nhận tiền đã lưu.</p>
              </div>
            )}
          </div>
        )}
        <SaveBar><SaveButton canSave={canSave} saving={saving} label="Lưu tài khoản" /></SaveBar>
      </form>
    </SettingsSection>
  )
}

// ── Báo giá ────────────────────────────────────────────────────────────────────

const quotationSchema = z.object({
  quotationPrefix: z.string().regex(/^[A-Z]{1,5}$/, 'Chỉ 1–5 chữ hoa A-Z'),
  defaultValidityDays: z.coerce.number().int().min(1, 'Từ 1 đến 365').max(365, 'Từ 1 đến 365'),
  defaultDepositPercent: z.coerce.number().int().min(0, 'Từ 0 đến 100').max(100, 'Từ 0 đến 100'),
  defaultTerms: z.string(),
})

export function QuotationDefaultsSection({ settings }: { settings: Settings }) {
  const { form, onSubmit, saving, canSave } = useSettingsSection({
    dirtyKey: 'quotation',
    settings,
    schema: quotationSchema,
    // number inputs hold strings; keeping the baseline as strings makes "typed the same value back" not dirty
    pick: s => ({
      quotationPrefix: s.quotationPrefix,
      defaultValidityDays: String(s.defaultValidityDays),
      defaultDepositPercent: String(s.defaultDepositPercent),
      defaultTerms: s.defaultTerms,
    }),
    savedMessage: 'Đã lưu mặc định báo giá',
  })
  const { register, watch, setValue, formState: { errors } } = form

  return (
    <SettingsSection id="settings-quotation" title="Mặc định báo giá" description="Áp dụng cho các báo giá mới tạo; báo giá đã gửi không đổi.">
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <div className="grid gap-5 sm:grid-cols-3">
          <Field id="settings-quotationPrefix" label="Tiền tố số BG" error={errors.quotationPrefix?.message}>
            <Input id="settings-quotationPrefix" placeholder="BG" className="uppercase" {...a11y('settings-quotationPrefix', errors.quotationPrefix)} {...register('quotationPrefix')} />
          </Field>
          <Field id="settings-defaultValidityDays" label="Hiệu lực (ngày)" error={errors.defaultValidityDays?.message}>
            <Input id="settings-defaultValidityDays" type="number" min={1} max={365} {...a11y('settings-defaultValidityDays', errors.defaultValidityDays)} {...register('defaultValidityDays')} />
          </Field>
          <Field id="settings-defaultDepositPercent" label="Đặt cọc (%)" error={errors.defaultDepositPercent?.message}>
            <Input id="settings-defaultDepositPercent" type="number" min={0} max={100} {...a11y('settings-defaultDepositPercent', errors.defaultDepositPercent)} {...register('defaultDepositPercent')} />
          </Field>
        </div>
        <Field
          id="settings-defaultTerms"
          label="Điều khoản mặc định"
          hint="Chép vào mỗi báo giá mới; sửa được trong từng bản nháp."
          error={errors.defaultTerms?.message}
        >
          <Textarea id="settings-defaultTerms" rows={5} {...register('defaultTerms')} />
        </Field>
        {!watch('defaultTerms')?.trim() && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setValue('defaultTerms', DEFAULT_TERMS_TEMPLATE, { shouldDirty: true })}
          >
            Dùng điều khoản mẫu
          </Button>
        )}
        <SaveBar><SaveButton canSave={canSave} saving={saving} label="Lưu mặc định" /></SaveBar>
      </form>
    </SettingsSection>
  )
}

// ── Khách hàng & Portal ────────────────────────────────────────────────────────

const portalSchema = z.object({ portalResponseTime: z.string().max(60, 'Tối đa 60 ký tự') })

export function PortalResponseSection({ settings }: { settings: Settings }) {
  const { form, onSubmit, saving, canSave } = useSettingsSection({
    dirtyKey: 'portal',
    settings,
    schema: portalSchema,
    pick: s => ({ portalResponseTime: s.portalResponseTime ?? '' }),
    savedMessage: 'Đã lưu thời gian phản hồi',
  })
  const { register, formState: { errors } } = form

  return (
    <SettingsSection id="settings-portal" title="Thời gian phản hồi" description="Khách thấy câu này trên portal sau khi gửi yêu cầu.">
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <Field
          id="settings-portalResponseTime"
          label="Thời gian phản hồi dự kiến (hiện cho khách)"
          hint="Ví dụ: trong 24 giờ, trong 2 giờ làm việc. Để trống = “trong 24 giờ”."
          error={errors.portalResponseTime?.message}
        >
          <Input id="settings-portalResponseTime" maxLength={60} {...a11y('settings-portalResponseTime', errors.portalResponseTime)} {...register('portalResponseTime')} />
        </Field>
        <SaveBar><SaveButton canSave={canSave} saving={saving} label="Lưu thời gian phản hồi" /></SaveBar>
      </form>
    </SettingsSection>
  )
}

export function RequestTemplatesCard() {
  const { data, isLoading, isError } = useRequestTemplates()
  const active = data?.filter(t => t.active).length ?? 0

  return (
    <SettingsSection id="settings-request-templates" title="Mẫu yêu cầu" description="Khách chọn mẫu khi gửi yêu cầu trên portal hoặc qua form khách mới.">
      <div className="flex flex-wrap items-center justify-between gap-4">
        {isLoading ? <Skeleton className="h-6 w-40" /> : isError || !data ? (
          <p className="text-sm text-muted-foreground">Không tải được danh sách mẫu.</p>
        ) : (
          <p className="text-sm text-foreground">
            <span className="font-display text-display-md font-bold tabular-nums">{data.length}</span>
            <span className="ml-2 text-muted-foreground">mẫu · {active} đang dùng</span>
          </p>
        )}
        <Button asChild variant="outline">
          <Link to="/admin/settings/request-templates">Quản lý mẫu yêu cầu →</Link>
        </Button>
      </div>
    </SettingsSection>
  )
}

// ── Tài khoản ──────────────────────────────────────────────────────────────────

const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Bắt buộc'),
  newPassword: z.string().min(12, 'Tối thiểu 12 ký tự'),
  confirmPassword: z.string().min(1, 'Bắt buộc'),
}).refine(d => d.newPassword === d.confirmPassword, {
  message: 'Mật khẩu không khớp',
  path: ['confirmPassword'],
})
type PasswordForm = z.infer<typeof passwordSchema>

export function PasswordSection() {
  const { register, handleSubmit, setError, reset, formState: { errors, isDirty } } = useForm<PasswordForm>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })
  useReportDirty('account', isDirty)

  const changePw = useMutation({
    mutationFn: ({ currentPassword, newPassword }: PasswordForm) =>
      api.post('/admin/account/password', { currentPassword, newPassword }),
    onSuccess: () => {
      toast.success('Đã đổi mật khẩu')
      reset()
    },
    onError: (err: ApiError) => {
      if (err.status === 401) setError('currentPassword', { message: 'Mật khẩu hiện tại không đúng' })
      else if (!applyServerErrors(err, setError)) toast.error('Đổi mật khẩu thất bại. Vui lòng thử lại.')
    },
  })

  return (
    <SettingsSection id="settings-password" title="Đổi mật khẩu" description="Mật khẩu mới tối thiểu 12 ký tự.">
      <form onSubmit={handleSubmit(data => changePw.mutate(data))} noValidate className="space-y-5">
        <Field id="pw-current" label="Mật khẩu hiện tại" error={errors.currentPassword?.message}>
          <Input id="pw-current" type="password" autoComplete="current-password" {...a11y('pw-current', errors.currentPassword)} {...register('currentPassword')} />
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field id="pw-new" label="Mật khẩu mới" error={errors.newPassword?.message}>
            <Input id="pw-new" type="password" autoComplete="new-password" {...a11y('pw-new', errors.newPassword)} {...register('newPassword')} />
          </Field>
          <Field id="pw-confirm" label="Xác nhận mật khẩu" error={errors.confirmPassword?.message}>
            <Input id="pw-confirm" type="password" autoComplete="new-password" {...a11y('pw-confirm', errors.confirmPassword)} {...register('confirmPassword')} />
          </Field>
        </div>
        <SaveBar>
          <Button type="submit" disabled={!isDirty || changePw.isPending} className="min-w-36">
            {changePw.isPending ? 'Đang xử lý…' : 'Đổi mật khẩu'}
          </Button>
        </SaveBar>
      </form>
    </SettingsSection>
  )
}
