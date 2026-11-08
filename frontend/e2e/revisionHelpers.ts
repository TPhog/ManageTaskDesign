import type { Page } from '@playwright/test'
import { apiPost } from './helpers'

/** Deliverable with an explicit quota and price per extra round; returns its id. */
export async function createQuotaDeliverable(
  page: Page, projectId: string, name: string, includedRounds: number, extraRoundPrice = 200000,
): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`,
    { name, quantity: 1, includedRounds, extraRoundPrice })).id
}

/** Request recorded by the designer (source ZALO); returns its id. */
export async function createRequest(page: Page, projectId: string, content: string, deliverableId: string | null = null): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/projects/${projectId}/requests`,
    { content, source: 'ZALO', deliverableId })).request.id
}

/** Records and triages a request into one round of MINOR items with the suggested billing. */
export async function triageRequest(page: Page, projectId: string, deliverableId: string | null, descriptions: string[]) {
  const id = await createRequest(page, projectId, descriptions.join('\n'), deliverableId)
  const detail = await (await page.request.get(`/api/admin/requests/${id}`)).json()
  return apiPost(page.request, page, `/api/admin/requests/${id}/triage`, {
    rounds: [{ deliverableId, receivedAt: null, items: descriptions.map(description => ({ description, type: 'MINOR', billing: null })) }],
    rowVersion: detail.request.rowVersion,
  })
}

/** Records and triages a request into one round of the given item types with the suggested billing. */
export async function triageItems(page: Page, projectId: string, deliverableId: string | null, items: { description: string; type: string }[]) {
  const id = await createRequest(page, projectId, items.map(i => i.description).join('\n'), deliverableId)
  const detail = await (await page.request.get(`/api/admin/requests/${id}`)).json()
  return apiPost(page.request, page, `/api/admin/requests/${id}/triage`, {
    rounds: [{ deliverableId, receivedAt: null, items: items.map(i => ({ description: i.description, type: i.type, billing: null })) }],
    rowVersion: detail.request.rowVersion,
  })
}
