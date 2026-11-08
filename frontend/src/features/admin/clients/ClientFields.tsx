import type { FieldErrors, UseFormRegister } from 'react-hook-form'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { ClientForm } from './clientForm'

interface Props {
  register: UseFormRegister<ClientForm>
  errors: FieldErrors<ClientForm>
  /** keeps ids unique when the create and edit sheets exist at once */
  idPrefix?: string
}

/** Fields of the create (ClientsPage) and edit (ClientDetailPage) sheets. */
export function ClientFields({ register, errors, idPrefix = '' }: Props) {
  const id = (name: string) => `${idPrefix}${name}`
  return (
    <>
      <div className="space-y-2">
        <Label htmlFor={id('name')}>Tên khách <span className="text-destructive">*</span></Label>
        <Input
          id={id('name')}
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? id('name-error') : undefined}
          {...register('name')}
        />
        {errors.name && <p id={id('name-error')} className="text-sm text-destructive">{errors.name.message}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor={id('company')}>Công ty</Label>
        <Input id={id('company')} {...register('company')} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={id('phone')}>Số điện thoại</Label>
          <Input id={id('phone')} type="tel" inputMode="tel" {...register('phone')} />
        </div>
        <div className="space-y-2">
          <Label htmlFor={id('email')}>Email</Label>
          <Input
            id={id('email')}
            type="email"
            aria-invalid={!!errors.email}
            aria-describedby={errors.email ? id('email-error') : undefined}
            {...register('email')}
          />
          {errors.email && <p id={id('email-error')} className="text-sm text-destructive">{errors.email.message}</p>}
        </div>
      </div>
      <div className="space-y-2">
        <Label htmlFor={id('contactChannel')}>Kênh liên hệ</Label>
        <Input id={id('contactChannel')} placeholder="Zalo, Facebook…" {...register('contactChannel')} />
      </div>
      <div className="space-y-2">
        <Label htmlFor={id('notes')}>Ghi chú nội bộ</Label>
        <Textarea id={id('notes')} rows={3} {...register('notes')} />
        <p className="text-xs text-muted-foreground">Khách không nhìn thấy ghi chú này.</p>
      </div>
    </>
  )
}
