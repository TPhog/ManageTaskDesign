// Plan 13 — request templates + public intake (see plan "API Contract"). Shared by admin, portal and the public form.
import type { DesignType } from './types'

export type TemplateFieldType = 'SHORT_TEXT' | 'LONG_TEXT' | 'SINGLE_CHOICE' | 'MULTI_CHOICE' | 'SIZE' | 'DATE' | 'NUMBER' | 'IMAGES'
export type SizeUnit = 'px' | 'mm' | 'cm' | 'm'

export interface TemplateField {
  key: string
  label: string
  type: TemplateFieldType
  required: boolean
  help: string | null
  options: string[]
}
export interface TemplateAnswer { key: string; label: string; type: TemplateFieldType; display: string }

/** public / portal — no serviceId, designType or active */
export interface PublicRequestTemplate { id: string; name: string; description: string; fields: TemplateField[] }
export interface AdminRequestTemplate extends PublicRequestTemplate {
  designType: DesignType
  serviceId: string | null
  serviceName: string | null
  active: boolean
  sortOrder: number
  rowVersion: number
  createdAt: string
  updatedAt: string
}

/** value of one key in the multipart "answers" JSON object */
export type AnswerValue = string | string[] | number | { preset: string } | { width: number; height: number; unit: SizeUnit }

export interface RequestTemplateBody {
  name: string
  description: string
  designType: DesignType
  serviceId: string | null
  active: boolean
  fields: TemplateField[]
  rowVersion?: number
}

export interface IntakeLink { enabled: boolean; slug: string | null; url: string | null }
export interface PublicIntakeForm {
  studio: { displayName: string; contactPhone: string | null; contactEmail: string | null }
  templates: PublicRequestTemplate[]
  /** 0 when the studio storage is nearly full: the form takes text only */
  maxImages: number
  maxImageMb: number
  formToken: string
}

export type IntakeStatus = 'NEW' | 'CONVERTED' | 'REJECTED'
export interface IntakeImage {
  id: string; width: number; height: number; sizeBytes: number; createdAt: string
  thumbUrl: string; previewUrl: string; originalUrl: string
}
export interface IntakeListItem {
  id: string; createdAt: string; status: IntakeStatus
  contactName: string; contactPhone: string; contactEmail: string | null
  templateName: string | null; excerpt: string; imageCount: number
}
export interface IntakeMatchingClient { id: string; name: string; phone: string | null; email: string | null }
export interface IntakeDetail extends IntakeListItem {
  templateId: string | null
  answers: TemplateAnswer[]
  note: string
  body: string
  images: IntakeImage[]
  suggestedDesignType: DesignType
  suggestedServiceId: string | null
  suggestedServiceName: string | null
  suggestedDueDate: string | null
  rejectReason: string | null
  rejectedAt: string | null
  convertedClientId: string | null
  convertedProjectId: string | null
  convertedAt: string | null
  matchingClients: IntakeMatchingClient[]
  rowVersion: number
}
export interface IntakePage { items: IntakeListItem[]; page: number; pageSize: number; total: number }
export interface ConvertIntakeBody {
  existingClientId: string | null; clientName: string | null; projectName: string; designType: DesignType
  dueDate: string | null; serviceId: string | null; deliverableName: string | null; rowVersion: number
}
export interface ConvertIntakeResult { clientId: string; projectId: string; deliverableId: string | null }
