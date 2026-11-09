// Plan 08 — image & storage API (see plan "API Contract"). URLs are API paths usable directly in <img src>.
import type { ProjectStatus } from './types'

export type ImageOwnerType = 'TASK' | 'PROGRESS_UPDATE' | 'CLIENT_REQUEST' | 'DELIVERABLE' | 'INTAKE_SUBMISSION'
export type StorageLevel = 'OK' | 'NOTICE' | 'WARNING' | 'BLOCKED' | 'CRITICAL'

/** P10: an earlier image in the project with the same content (SHA-256). */
export interface ImageDuplicate {
  id: string
  ownerType: ImageOwnerType
  ownerId: string
  taskTitle: string | null
  createdAt: string
  sameUpload: boolean
}

export interface AdminImage {
  id: string
  projectId: string
  ownerType: ImageOwnerType
  ownerId: string
  originalName: string
  width: number
  height: number
  sizeBytes: number
  visibleToClient: boolean
  uploadedBy: 'DESIGNER' | 'CLIENT'
  createdAt: string
  thumbUrl: string
  previewUrl: string
  originalUrl: string
  originalDeletedAt: string | null
  duplicateOf: ImageDuplicate | null
}

/** Portal image: no original, file name, owner or task (TSK-05). */
export interface PortalImage {
  id: string
  source: 'TASK' | 'PROGRESS_UPDATE' | 'CLIENT_REQUEST'
  deliverableId: string | null
  deliverableName: string | null
  width: number
  height: number
  createdAt: string
  thumbUrl: string
  previewUrl: string
}

export interface ProgressUpdateResult {
  id: string
  occurredAt: string
  message: string
  images: AdminImage[]
}

export interface StorageUsage {
  usedBytes: number
  quotaBytes: number
  percent: number
  level: StorageLevel
  uploadsBlocked: boolean
  thresholds: number[]
}

export interface StorageImage {
  id: string
  projectId: string
  projectName: string
  projectStatus: ProjectStatus
  clientName: string
  ownerType: ImageOwnerType
  ownerId: string
  /** null when not a task image or the task was deleted */
  taskTitle: string | null
  originalName: string
  width: number
  height: number
  sizeBytes: number
  visibleToClient: boolean
  uploadedBy: 'DESIGNER' | 'CLIENT'
  createdAt: string
  thumbUrl: string
  previewUrl: string
  originalDeletedAt: string | null
}

export interface StorageImagePage {
  items: StorageImage[]
  page: number
  pageSize: number
  total: number
  totalBytes: number
}

export interface StorageImageParams {
  projectId?: string
  completedOnly?: boolean
  olderThanMonths?: number
  sort?: 'size' | 'oldest' | 'newest'
  page?: number
  pageSize?: number
}

export interface WatermarkSettings {
  enabled: boolean
  text: string | null
  effectiveText: string
  opacityPercent: number
}

export interface BulkDeleteResult {
  deleted: number
  skipped: number
  freedBytes: number
}

/** What ImageGrid / ImageLightbox show — the same shape on admin and portal. */
export interface LightboxImage {
  id: string
  src: string
  thumbSrc: string
  alt: string
  caption?: string
}

/** P10 — Settings › Dung lượng › Theo dự án. completedAt only for COMPLETED projects. */
export interface StorageProject {
  projectId: string
  projectName: string
  clientName: string
  projectStatus: ProjectStatus
  completedAt: string | null
  imageCount: number
  totalBytes: number
  /** originals still stored that a cleanup could free */
  originalBytes: number
  originalsDeleted: number
}

export interface OriginalCleanupProject {
  projectId: string
  projectName: string
  completedAt: string
  imageCount: number
  freedBytes: number
}

export interface OriginalCleanupPreview {
  olderThanMonths: number
  cutoff: string
  projectCount: number
  imageCount: number
  freedBytes: number
  /** SHA-256 of the candidate id set; echoed back on the run so it acts only on what was previewed */
  digest: string
  projects: OriginalCleanupProject[]
}

export interface OriginalCleanupResult {
  projectCount: number
  imageCount: number
  freedBytes: number
}
