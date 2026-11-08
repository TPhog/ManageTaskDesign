import { type Page } from '@playwright/test'
import { apiPost, createClient, createProjectForClient } from './helpers'
import { apiPut, configureBank, sendQuotation } from './portalHelpers'

/**
 * Same setup as project-flow.spec: service with a 3-step template, one deliverable on it, brief confirmed,
 * INITIAL quotation sent and accepted (deposit unpaid) → stage EXECUTE offering START_PROJECT.
 */
export async function quotedProject(page: Page, stamp: number): Promise<string> {
  await page.goto('/admin')
  await configureBank(page)
  const service = await apiPost(page.request, page, '/api/admin/services', {
    name: `Logo mẫu ${stamp}`, description: '', basePrice: 2_000_000, defaultIncludedRounds: 2, extraRoundPrice: 200_000,
    taskTemplate: ['Moodboard', 'Phác thảo', 'Hoàn thiện'],
  })
  const clientId = await createClient(page, `Khách luồng ${stamp}`)
  const projectId = await createProjectForClient(page, clientId, `Dự án luồng ${stamp}`)
  await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name: 'Logo E2E', quantity: 1, serviceId: service.id })
  const detail = await (await page.request.get(`/api/admin/projects/${projectId}`)).json()
  // an empty brief can no longer be confirmed (422) — give it content first
  await apiPut(page, `/api/admin/briefs/${detail.brief.id}`, { content: 'Brief E2E: logo tối giản, tông ấm.' })
  await apiPost(page.request, page, `/api/admin/briefs/${detail.brief.id}/confirm`, {})
  const quotation = await sendQuotation(page, projectId, 'SECRET-FLOW')
  const q = await (await page.request.get(`/api/admin/quotations/${quotation.id}`)).json()
  await apiPost(page.request, page, `/api/admin/quotations/${quotation.id}/accept`, { rowVersion: q.rowVersion })
  return projectId
}
