import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronsUpDown } from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
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
  bankAccountNo: z.string().nullable(),
  bankAccountName: z.string().nullable(),
  quotationPrefix: z.string().regex(/^[A-Z]{1,5}$/, 'Chỉ 1–5 chữ hoa A-Z'),
  defaultValidityDays: z.coerce.number().int().min(1).max(365),
  defaultDepositPercent: z.coerce.number().int().min(0).max(100),
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
  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<SettingsForm>({
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
    mutationFn: (body: SettingsForm & { rowVersion: number }) => api.put('/admin/settings', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      toast.success('Đã lưu cài đặt')
    },
    onError: () => toast.error('Lưu thất bại. Vui lòng thử lại.'),
  })

  const { register: regPw, handleSubmit: handlePwSubmit, formState: { errors: pwErrors }, reset: resetPw } = useForm<PasswordForm>({
    resolver: zodResolver(passwordSchema),
  })

  const changePwMutation = useMutation({
    mutationFn: (body: PasswordForm) => api.post('/auth/change-password', body),
    onSuccess: () => {
      toast.success('Đã đổi mật khẩu')
      resetPw()
    },
    onError: () => toast.error('Đổi mật khẩu thất bại. Kiểm tra mật khẩu hiện tại.'),
  })

  if (loadingSettings) {
    return (
      <div className="max-w-2xl space-y-4">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  return (
    <div className="max-w-2xl space-y-8">
      <h1 className="text-xl font-semibold">Cài đặt</h1>

      <form onSubmit={handleSubmit(data => updateMutation.mutate({ ...data, rowVersion: settings!.rowVersion }))} className="space-y-6">
        {/* Hồ sơ */}
        <section className="space-y-4">
          <h2 className="text-sm font-medium text-gray-500 uppercase tracking-wide">Hồ sơ</h2>
          <div className="space-y-1">
            <Label>Tên hiển thị</Label>
            <Input {...register('displayName')} />
            {errors.displayName && <p className="text-sm text-red-500">{errors.displayName.message}</p>}
          </div>
          <div className="space-y-1">
            <Label>Email liên hệ</Label>
            <Input type="email" {...register('contactEmail')} />
            {errors.contactEmail && <p className="text-sm text-red-500">{errors.contactEmail.message}</p>}
          </div>
          <div className="space-y-1">
            <Label>Số điện thoại</Label>
            <Input {...register('contactPhone')} />
          </div>
        </section>

        <Separator />

        {/* Ngân hàng */}
        <section className="space-y-4">
          <h2 className="text-sm font-medium text-gray-500 uppercase tracking-wide">Ngân hàng</h2>
          <div className="space-y-1">
            <Label>Ngân hàng</Label>
            <Popover open={bankOpen} onOpenChange={setBankOpen}>
              <PopoverTrigger asChild>
                <Button variant="outline" role="combobox" className="w-full justify-between font-normal">
                  {selectedBank ? `${selectedBank.shortName} — ${selectedBank.name}` : 'Chọn ngân hàng...'}
                  <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-full p-0">
                <Command>
                  <CommandInput placeholder="Tìm theo tên hoặc BIN..." />
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
          </div>
          <div className="space-y-1">
            <Label>Số tài khoản</Label>
            <Input {...register('bankAccountNo')} />
          </div>
          <div className="space-y-1">
            <Label>Tên chủ tài khoản <span className="text-xs text-gray-400">(in hoa)</span></Label>
            <Input {...register('bankAccountName')} placeholder="NGUYEN VAN A" className="uppercase" />
          </div>
          {selectedBin && bankAccountNo && (
            <div className="space-y-2">
              <Button type="button" variant="outline" size="sm" onClick={() => setShowQr(v => !v)}>
                {showQr ? 'Ẩn QR' : 'Thử QR 10.000 ₫'}
              </Button>
              {showQr && (
                <div className="space-y-1">
                  <img src="/api/admin/settings/test-qr.png" alt="Test QR" className="w-48 h-48 border rounded" />
                  <p className="text-xs text-gray-500">Quét để kiểm tra tài khoản nhận tiền của bạn</p>
                </div>
              )}
            </div>
          )}
        </section>

        <Separator />

        {/* Mặc định báo giá */}
        <section className="space-y-4">
          <h2 className="text-sm font-medium text-gray-500 uppercase tracking-wide">Mặc định báo giá</h2>
          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-1">
              <Label>Tiền tố số BG</Label>
              <Input {...register('quotationPrefix')} placeholder="BG" className="uppercase" />
              {errors.quotationPrefix && <p className="text-sm text-red-500">{errors.quotationPrefix.message}</p>}
            </div>
            <div className="space-y-1">
              <Label>Hiệu lực (ngày)</Label>
              <Input type="number" min={1} max={365} {...register('defaultValidityDays')} />
              {errors.defaultValidityDays && <p className="text-sm text-red-500">{errors.defaultValidityDays.message}</p>}
            </div>
            <div className="space-y-1">
              <Label>Đặt cọc (%)</Label>
              <Input type="number" min={0} max={100} {...register('defaultDepositPercent')} />
              {errors.defaultDepositPercent && <p className="text-sm text-red-500">{errors.defaultDepositPercent.message}</p>}
            </div>
          </div>
          <div className="space-y-1">
            <Label>Điều khoản mặc định</Label>
            <Textarea rows={4} {...register('defaultTerms')} />
          </div>
        </section>

        <Button type="submit" disabled={updateMutation.isPending}>
          {updateMutation.isPending ? 'Đang lưu...' : 'Lưu thay đổi'}
        </Button>
      </form>

      <Separator />

      {/* Đổi mật khẩu */}
      <section className="space-y-4">
        <h2 className="text-sm font-medium text-gray-500 uppercase tracking-wide">Đổi mật khẩu</h2>
        <form onSubmit={handlePwSubmit(data => changePwMutation.mutate(data))} className="space-y-4 max-w-sm">
          <div className="space-y-1">
            <Label>Mật khẩu hiện tại</Label>
            <Input type="password" {...regPw('currentPassword')} />
            {pwErrors.currentPassword && <p className="text-sm text-red-500">{pwErrors.currentPassword.message}</p>}
          </div>
          <div className="space-y-1">
            <Label>Mật khẩu mới</Label>
            <Input type="password" {...regPw('newPassword')} />
            {pwErrors.newPassword && <p className="text-sm text-red-500">{pwErrors.newPassword.message}</p>}
          </div>
          <div className="space-y-1">
            <Label>Xác nhận mật khẩu</Label>
            <Input type="password" {...regPw('confirmPassword')} />
            {pwErrors.confirmPassword && <p className="text-sm text-red-500">{pwErrors.confirmPassword.message}</p>}
          </div>
          <Button type="submit" variant="outline" disabled={changePwMutation.isPending}>
            {changePwMutation.isPending ? 'Đang xử lý...' : 'Đổi mật khẩu'}
          </Button>
        </form>
      </section>
    </div>
  )
}
