import type { ConvertIntakeBody, IntakeDetail, IntakeStatus } from '@/lib/requestTemplateTypes'
import type { DesignType } from '@/lib/types'

export const intakeStatusLabel: Record<IntakeStatus, string> = {
  NEW: 'Mới', CONVERTED: 'Đã chuyển thành dự án', REJECTED: 'Đã từ chối',
}
/** StatusBadge has no CONVERTED style: reuse the existing green/red ones. */
export const intakeStatusBadge: Record<IntakeStatus, string> = { NEW: 'NEW', CONVERTED: 'ACCEPTED', REJECTED: 'REJECTED' }

export interface ConvertFormValues {
  clientMode: 'new' | 'existing'
  existingClientId: string
  clientName: string
  projectName: string
  designType: DesignType
  /** 'YYYY-MM-DD' or '' */
  dueDate: string
  /** '' = no deliverable */
  serviceId: string
  deliverableName: string
}

/** Never links to an existing client by itself — the designer picks "Khách có sẵn" (BR-01). */
export function defaultConvertValues(d: IntakeDetail): ConvertFormValues {
  return {
    clientMode: 'new',
    existingClientId: d.matchingClients[0]?.id ?? '',
    clientName: d.contactName,
    projectName: `${d.templateName ?? 'Yêu cầu'} — ${d.contactName}`.slice(0, 200),
    designType: d.suggestedDesignType,
    dueDate: d.suggestedDueDate ?? '',
    serviceId: d.suggestedServiceId ?? '',
    deliverableName: d.suggestedServiceName ?? '',
  }
}

export function toConvertBody(v: ConvertFormValues, rowVersion: number): ConvertIntakeBody {
  const existing = v.clientMode === 'existing' && v.existingClientId !== ''
  return {
    existingClientId: existing ? v.existingClientId : null,
    clientName: existing ? null : v.clientName.trim(),
    projectName: v.projectName.trim(),
    designType: v.designType,
    dueDate: v.dueDate || null,
    serviceId: v.serviceId || null,
    deliverableName: v.serviceId && v.deliverableName.trim() ? v.deliverableName.trim() : null,
    rowVersion,
  }
}

export function telHref(phone: string): string {
  return `tel:${phone.trim().replace(/[^\d+]/g, '')}`
}

/** zalo.me takes the national number: +84 / 84… → 0… */
export function zaloHref(phone: string): string {
  let digits = phone.replace(/\D/g, '')
  if (digits.startsWith('84') && digits.length >= 11) digits = `0${digits.slice(2)}`
  return `https://zalo.me/${digits}`
}

const NOTE_MARK = '\n\nGhi chú thêm:\n'

/** The note part of TemplateRenderer.Render's text: answer lines are indented, so the FIRST header is the real one (a note may repeat it). */
export function templateNote(content: string): string | null {
  const at = content.indexOf(NOTE_MARK)
  return at >= 0 ? content.slice(at + NOTE_MARK.length) : null
}
