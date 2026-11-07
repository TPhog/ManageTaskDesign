import { useState, type ReactNode } from 'react'
import { useForm, type FieldValues, type Path, type UseFormSetError } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronsUpDown } from 'lucide-react'
import { toast } from 'sonner'
import { api, emptyToNull, type ApiError } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/PageHeader'
import { cn } from '@/lib/utils'

interface Bank { bin: string; code: string; shortName: string; name: string }
interface Settings {
  displayName: string
  contactEmail: string | null
  contactPhone: string | null
  bankBin: string | null
  bankAccountNo: string | null
  bankAccountName: string | null
  quotationPrefix: string
  defaultValidityDays: number
  defaultDepositPercent: number
  defaultTerms: string
  rowVersion: number
}

const settingsSchema = z.object({
  displayName: z.string().min(1, 'Bắt buộc'),
  contactEmail: z.string().email('Email không hợp lệ').or(z.literal('')).nullable(),
  contactPhone: z.string().nullable(),
  bankBin: z.string().nullable(),
  bankAccountNo: z.string().regex(/^[A-Za-z0-9]{4,19}$/, '4–19 ký tự chữ hoặc số').or(z.literal('')).nullable(),
  bankAccountName: z.string().nullable(),
  quotationPrefix: z.string().regex(/^[A-Z]{1,5}$/, 'Chỉ 1–5 chữ hoa A-Z'),
  defaultValidityDays: z.coerce.number().int().min(1, 'Từ 1 đến 365').max(365, 'Từ 1 đến 365'),
  defaultDepositPercent: z.coerce.number().int().min(0, 'Từ 0 đến 100').max(100, 'Từ 0 đến 100'),
  defaultTerms: z.string(),
})
type SettingsForm = z.infer<typeof settingsSchema>

const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Bắt buộc'),
  newPassword: z.string().min(12, 'Tối thiểu 12 ký tự'),
  confirmPassword: z.string().min(1, 'Bắt buộc'),
}).refine(d => d.newPassword === d.confirmPassword, {
  message: 'Mật khẩu không khớp',
  path: ['confirmPassword'],
})
type PasswordForm = z.infer<typeof passwordSchema>

/** Copy ValidationProblem field errors from the API onto the matching form fields. Returns true if any matched. */
function applyServerErrors<T extends FieldValues>(err: ApiError, setError: UseFormSetError<T>): boolean {
  if (!err.errors) return false
  for (const [field, messages] of Object.entries(err.errors)) {
    setError(field as Path<T>, { message: messages[0] })
  }
  return Object.keys(err.errors).length > 0
}

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <section className="grid gap-6 rounded-xl border bg-card p-6 shadow-card md:grid-cols-3">
      <div>
        <h2 className="font-semibold text-foreground">{title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </div>
      <div className="space-y-5 md:col-span-2">{children}</div>
    </section>
  )
}

function Field({ id, label, hint, error, children }: {
  id: string
  label: ReactNode
  hint?: string
  error?: string
  children: ReactNode
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? (
        <p id={`${id}-error`} className="text-sm text-destructive">{error}</p>
      ) : hint ? (
        <p className="text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  )
}

export default function SettingsPage() {
  const queryClient = useQueryClient()
  const [bankOpen, setBankOpen] = useState(false)
  const [showQr, setShowQr] = useState(false)

  const { data: settings, isLoading: loadingSettings } = useQuery({
    queryKey: ['settings'],
    queryFn: () => api.get<Settings>('/admin/settings'),
  })
  const { data: banks } = useQuery({
    queryKey: ['banks'],
    queryFn: () => api.get<Bank[]>('/admin/banks'),
  })

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { register, handleSubmit, watch, setValue, setError, formState: { errors } } = useForm<SettingsForm>({
    resolver: zodResolver(settingsSchema) as any,
    values: settings ? {
      displayName: settings.displayName,
      contactEmail: settings.contactEmail ?? '',
      contactPhone: settings.contactPhone ?? '',
      bankBin: settings.bankBin ?? '',
      bankAccountNo: settings.bankAccountNo ?? '',
      bankAccountName: settings.bankAccountName ?? '',
      quotationPrefix: settings.quotationPrefix,
      defaultValidityDays: settings.defaultValidityDays,
      defaultDepositPercent: settings.defaultDepositPercent,
      defaultTerms: settings.defaultTerms,
    } : undefined,
  })

  const selectedBin = watch('bankBin')
  const bankAccountNo = watch('bankAccountNo')
  const selectedBank = banks?.find(b => b.bin === selectedBin)

  const updateMutation = useMutation({
    // blank optional inputs must reach the API as null (e.g. '' fails the bank account format check)
    mutationFn: (body: SettingsForm & { rowVersion: number }) => api.put('/admin/settings', emptyToNull(body)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      toast.success('Đã lưu cài đặt')
    },
    onError: (err: ApiError) => {
      if (applyServerErrors(err, setError)) toast.error('Vui lòng kiểm tra lại các trường được đánh dấu.')
      else if (err.status === 409) toast.error('Cài đặt đã thay đổi ở nơi khác. Vui lòng tải lại trang.')
      else toast.error('Lưu thất bại. Vui lòng thử lại.')
    },
  })

  const {
    register: regPw,
    handleSubmit: handlePwSubmit,
    setError: setPwError,
    formState: { errors: pwErrors },
    reset: resetPw,
  } = useForm<PasswordForm>({
    resolver: zodResolver(passwordSchema),
  })

  const changePwMutation = useMutation({
    mutationFn: ({ currentPassword, newPassword }: PasswordForm) =>
      api.post('/admin/account/password', { currentPassword, newPassword }),
    onSuccess: () => {
      toast.success('Đã đổi mật khẩu')
      resetPw()
    },
    onError: (err: ApiError) => {
      if (err.status === 401) setPwError('currentPassword', { message: 'Mật khẩu hiện tại không đúng' })
      else if (!applyServerErrors(err, setPwError)) toast.error('Đổi mật khẩu thất bại. Vui lòng thử lại.')
    },
  })

  const a11y = (name: keyof SettingsForm) => ({
    'aria-invalid': !!errors[name],
    'aria-describedby': errors[name] ? `settings-${name}-error` : undefined,
  })

  if (loadingSettings) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-56 w-full rounded-xl" />
        <Skeleton className="h-56 w-full rounded-xl" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Cài đặt" description="Hồ sơ, tài khoản nhận tiền và mặc định cho báo giá." />

      <form
        onSubmit={handleSubmit(data => updateMutation.mutate({ ...data, rowVersion: settings!.rowVersion }))}
        className="space-y-6"
        noValidate
      >
        <Section title="Hồ sơ" description="Thông tin hiển thị trên báo giá gửi khách.">
          <Field id="settings-displayName" label="Tên hiển thị" error={errors.displayName?.message}>
            <Input id="settings-displayName" {...a11y('displayName')} {...register('displayName')} />
          </Field>
          <div className="grid gap-5 sm:grid-cols-2">
            <Field id="settings-contactEmail" label="Email liên hệ" error={errors.contactEmail?.message}>
              <Input id="settings-contactEmail" type="email" {...a11y('contactEmail')} {...register('contactEmail')} />
            </Field>
            <Field id="settings-contactPhone" label="Số điện thoại" error={errors.contactPhone?.message}>
              <Input id="settings-contactPhone" type="tel" inputMode="tel" {...a11y('contactPhone')} {...register('contactPhone')} />
            </Field>
          </div>
        </Section>

        <Section title="Ngân hàng" description="Dùng để tạo mã VietQR trên báo giá. Có thể để trống.">
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
                            setValue('bankBin', bank.bin)
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
              <Input id="settings-bankAccountNo" inputMode="numeric" {...a11y('bankAccountNo')} {...register('bankAccountNo')} />
            </Field>
            <Field
              id="settings-bankAccountName"
              label="Tên chủ tài khoản"
              hint="Viết hoa, không dấu"
              error={errors.bankAccountName?.message}
            >
              <Input
                id="settings-bankAccountName"
                placeholder="NGUYEN VAN A"
                className="uppercase"
                {...a11y('bankAccountName')}
                {...register('bankAccountName')}
              />
            </Field>
          </div>
          {selectedBin && bankAccountNo && (
            <div className="space-y-3">
              <Button type="button" variant="outline" size="sm" onClick={() => setShowQr(v => !v)}>
                {showQr ? 'Ẩn QR' : 'Thử QR 10.000 ₫'}
              </Button>
              {showQr && (
                <div className="flex items-center gap-4 rounded-lg border bg-muted/40 p-4">
                  <img src="/api/admin/settings/test-qr.png" alt="Mã QR thử 10.000 ₫" className="h-40 w-40 rounded-md border bg-white" />
                  <p className="text-sm text-muted-foreground">Quét bằng app ngân hàng để kiểm tra tài khoản nhận tiền của bạn.</p>
                </div>
              )}
            </div>
          )}
        </Section>

        <Section title="Mặc định báo giá" description="Áp dụng cho các báo giá mới tạo.">
          <div className="grid gap-5 sm:grid-cols-3">
            <Field id="settings-quotationPrefix" label="Tiền tố số BG" error={errors.quotationPrefix?.message}>
              <Input id="settings-quotationPrefix" placeholder="BG" className="uppercase" {...a11y('quotationPrefix')} {...register('quotationPrefix')} />
            </Field>
            <Field id="settings-defaultValidityDays" label="Hiệu lực (ngày)" error={errors.defaultValidityDays?.message}>
              <Input id="settings-defaultValidityDays" type="number" min={1} max={365} {...a11y('defaultValidityDays')} {...register('defaultValidityDays')} />
            </Field>
            <Field id="settings-defaultDepositPercent" label="Đặt cọc (%)" error={errors.defaultDepositPercent?.message}>
              <Input id="settings-defaultDepositPercent" type="number" min={0} max={100} {...a11y('defaultDepositPercent')} {...register('defaultDepositPercent')} />
            </Field>
          </div>
          <Field id="settings-defaultTerms" label="Điều khoản mặc định" error={errors.defaultTerms?.message}>
            <Textarea id="settings-defaultTerms" rows={4} {...register('defaultTerms')} />
          </Field>
        </Section>

        <div className="flex justify-end">
          <Button type="submit" disabled={updateMutation.isPending} className="min-w-36">
            {updateMutation.isPending ? 'Đang lưu…' : 'Lưu thay đổi'}
          </Button>
        </div>
      </form>

      <form onSubmit={handlePwSubmit(data => changePwMutation.mutate(data))} noValidate>
        <Section title="Đổi mật khẩu" description="Mật khẩu mới tối thiểu 12 ký tự.">
          <Field id="pw-current" label="Mật khẩu hiện tại" error={pwErrors.currentPassword?.message}>
            <Input id="pw-current" type="password" autoComplete="current-password" aria-invalid={!!pwErrors.currentPassword} {...regPw('currentPassword')} />
          </Field>
          <div className="grid gap-5 sm:grid-cols-2">
            <Field id="pw-new" label="Mật khẩu mới" error={pwErrors.newPassword?.message}>
              <Input id="pw-new" type="password" autoComplete="new-password" aria-invalid={!!pwErrors.newPassword} {...regPw('newPassword')} />
            </Field>
            <Field id="pw-confirm" label="Xác nhận mật khẩu" error={pwErrors.confirmPassword?.message}>
              <Input id="pw-confirm" type="password" autoComplete="new-password" aria-invalid={!!pwErrors.confirmPassword} {...regPw('confirmPassword')} />
            </Field>
          </div>
          <div className="flex justify-end">
            <Button type="submit" variant="outline" disabled={changePwMutation.isPending}>
              {changePwMutation.isPending ? 'Đang xử lý…' : 'Đổi mật khẩu'}
            </Button>
          </div>
        </Section>
      </form>
    </div>
  )
}
