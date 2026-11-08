import { type Browser, type Page, expect } from '@playwright/test'
import { apiPost, csrfToken } from './helpers'

/** PUT with the CSRF double-submit token + Origin that CsrfMiddleware requires; returns the JSON body (null on 204). */
export async function apiPut(page: Page, url: string, data: unknown) {
  const res = await page.request.put(url, {
    data,
    headers: { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin },
  })
  expect(res.ok(), `${url} → ${res.status()}`).toBeTruthy()
  return res.status() === 204 ? null : res.json()
}

/** Sending needs a receiving account (docs/06 §5). Only fills it when missing - never overwrite the designer's real account. */
export async function configureBank(page: Page) {
  const settings = await (await page.request.get('/api/admin/settings')).json()
  if (settings.bankBin && settings.bankAccountNo && settings.bankAccountName) return
  await apiPut(page, '/api/admin/settings', {
    ...settings,
    bankBin: '970436',
    bankAccountNo: '0123456789',
    bankAccountName: 'NGUYEN VAN A',
  })
}

export async function createService(page: Page, name: string, basePrice: number): Promise<string> {
  return (await apiPost(page.request, page, '/api/admin/services',
    { name, description: '', basePrice, defaultIncludedRounds: 2, extraRoundPrice: 100_000 })).id
}

interface DraftLine {
  id: string
  description: string
  details: string
  quantity: number
  unitPrice: number
  sourceType: string
  sourceId: string | null
}

/** INITIAL quotation from the project's deliverables, deposit 50%, with an internal note — then sent. */
export async function sendQuotation(page: Page, projectId: string, internalNotes: string): Promise<{ id: string; number: string }> {
  const { id } = await apiPost(page.request, page, `/api/admin/projects/${projectId}/quotations`,
    { kind: 'INITIAL', title: null, fromDeliverables: true })
  const draft = await (await page.request.get(`/api/admin/quotations/${id}`)).json()
  const saved = await apiPut(page, `/api/admin/quotations/${id}`, {
    title: draft.title,
    validityDays: 7,
    discountAmount: 0,
    vatPercent: 0,
    depositPercent: 50,
    terms: draft.terms,
    notesToClient: 'Cảm ơn anh/chị đã tin tưởng',
    internalNotes,
    items: (draft.items as DraftLine[]).map(i => ({
      id: i.id, description: i.description, details: i.details, quantity: i.quantity,
      unitPrice: i.unitPrice, sourceType: i.sourceType, sourceId: i.sourceId,
    })),
    rowVersion: draft.rowVersion,
  })
  const sent = await apiPost(page.request, page, `/api/admin/quotations/${id}/send`, { rowVersion: saved.rowVersion })
  return { id, number: sent.number }
}

/** Creates (or regenerates) the client's magic link; returns the URL shown once to the designer. */
export async function issuePortalLink(page: Page, clientId: string): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/clients/${clientId}/portal-link`, {})).url
}

/** The client's phone: a fresh context without the designer's cookies. */
export async function openAsClient(browser: Browser, url: string) {
  const context = await browser.newContext({
    storageState: { cookies: [], origins: [] },
    viewport: { width: 390, height: 844 },
  })
  const page = await context.newPage()
  // 'commit': the landing page navigates on to /portal by itself as soon as the token is exchanged
  await page.goto(url, { waitUntil: 'commit' })
  return { context, page }
}
