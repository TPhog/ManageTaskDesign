import type { StartPreviewItem, StartProjectBody } from '@/lib/flowTypes'

/** One row of the start dialog: a NOT_STARTED deliverable and its editable template steps. */
export interface StartDraft {
  deliverableId: string
  name: string
  serviceName: string | null
  titles: { title: string; checked: boolean }[]
}

export function draftsFromPreview(items: StartPreviewItem[]): StartDraft[] {
  return items.map(i => ({
    deliverableId: i.deliverableId,
    name: i.name,
    serviceName: i.serviceName,
    titles: i.titles.map(title => ({ title, checked: true })),
  }))
}

/** Body.tasks of POST /admin/projects/{id}/start: ticked, trimmed, non-empty titles only. */
export function toStartTasks(drafts: StartDraft[]): StartProjectBody['tasks'] {
  return drafts
    .map(d => ({
      deliverableId: d.deliverableId,
      titles: d.titles.filter(t => t.checked).map(t => t.title.trim()).filter(t => t.length > 0),
    }))
    .filter(d => d.titles.length > 0)
}
