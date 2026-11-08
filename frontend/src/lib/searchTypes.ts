import type { EffectiveStatus } from './quotationTypes'
import type { ProjectStatus } from './types'

/** GET /api/admin/search?q= (docs/08 §2.1) — at most 5 rows per group */
export interface SearchClient {
  id: string
  name: string
  company: string | null
  phone: string | null
  status: 'ACTIVE' | 'INACTIVE'
}

export interface SearchProject {
  id: string
  name: string
  clientName: string
  status: ProjectStatus
  dueDate: string | null
}

export interface SearchQuotation {
  id: string
  projectId: string
  number: string | null
  version: number
  title: string
  projectName: string
  clientName: string
  effectiveStatus: EffectiveStatus
  total: number
}

export interface SearchResult {
  clients: SearchClient[]
  projects: SearchProject[]
  quotations: SearchQuotation[]
}
