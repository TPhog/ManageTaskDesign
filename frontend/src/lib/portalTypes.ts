import type { DeliverableStatus, DesignType, ProjectStatus } from './types'

// Mirrors backend Features/Portal/PortalDtos.cs — portal DTOs never carry internal fields.

export interface PortalMe {
  clientName: string
  designer: {
    displayName: string
    logoUrl: string | null
    contactPhone: string | null
    contactEmail: string | null
  }
}

export interface PortalDeliverableSummary {
  /** Excludes CANCELLED */
  total: number
  delivered: number
  approved: number
}

export interface PortalProjectListItem {
  id: string
  name: string
  designType: DesignType
  status: ProjectStatus
  dueDate: string | null
  deliverableSummary: PortalDeliverableSummary
  hasOpenQuotation: boolean
}

export interface PortalDeliverable {
  id: string
  name: string
  quantity: number
  status: DeliverableStatus
  includedRounds: number
  usedRounds: number
  remainingRounds: number
  dueDate: string | null
}

export interface PortalBrief {
  version: number
  content: string
  confirmedAt: string | null
}

export interface PortalProjectDetail {
  id: string
  name: string
  designType: DesignType
  status: ProjectStatus
  /** Only present when the designer made the reason public */
  statusReason: string | null
  startDate: string | null
  dueDate: string | null
  deliverableSummary: PortalDeliverableSummary
  deliverables: PortalDeliverable[]
  brief: PortalBrief | null
}

export interface PortalTimelineItem {
  occurredAt: string
  message: string
}
