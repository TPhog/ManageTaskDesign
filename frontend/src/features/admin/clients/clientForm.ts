import { z } from 'zod'

export const clientSchema = z.object({
  name: z.string().min(1, 'Vui lòng nhập tên khách'),
  company: z.string().optional(),
  phone: z.string().optional(),
  email: z.string().email('Email không hợp lệ').optional().or(z.literal('')),
  contactChannel: z.string().optional(),
  notes: z.string().optional(),
})

export type ClientForm = z.infer<typeof clientSchema>
