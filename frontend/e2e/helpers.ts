import { type APIRequestContext, type Page, expect } from '@playwright/test'

// Seeded by appsettings.Development.json (Seed:DesignerUsername / Seed:DesignerPassword)
export const TEST_USERNAME = process.env.E2E_USERNAME ?? 'designer'
export const TEST_PASSWORD = process.env.E2E_PASSWORD ?? 'password123456'
export const API_BASE = process.env.E2E_API_BASE ?? 'http://localhost:5261'

/** Saved designer session, produced by auth.setup.ts and reused by every spec except auth.spec.ts */
export const STORAGE_STATE = 'e2e/.auth/designer.json'

export async function login(page: Page) {
  await page.goto('/admin/login')
  await page.getByLabel('Tên đăng nhập').fill(TEST_USERNAME)
  await page.getByLabel('Mật khẩu').fill(TEST_PASSWORD)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await expect(page).toHaveURL(/\/admin$/)
}

/** Wait for a toast with given text substring */
export async function expectToast(page: Page, text: string | RegExp) {
  await expect(page.locator('[data-sonner-toast]').filter({ hasText: text }).first()).toBeVisible({ timeout: 5000 })
}

/** Pick an option of a Radix Select identified by its accessible name */
export async function selectOption(page: Page, combobox: string | RegExp, option: string | RegExp) {
  await page.getByRole('combobox', { name: combobox }).click()
  await page.getByRole('option', { name: option }).click()
}

export async function csrfToken(page: Page) {
  const cookie = (await page.context().cookies()).find(c => c.name === 'ff_csrf')
  return cookie ? decodeURIComponent(cookie.value) : ''
}

export async function apiPost(request: APIRequestContext, page: Page, url: string, data: unknown) {
  // CsrfMiddleware requires Origin == App:PublicBaseUrl plus the double-submit token;
  // APIRequestContext sends neither on its own. page must already be on an app URL.
  const res = await request.post(url, {
    data,
    headers: { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin },
  })
  expect(res.ok(), `${url} → ${res.status()}`).toBeTruthy()
  return res.json()
}

/** Make sure at least one active client exists; returns its id. */
export async function ensureClient(page: Page): Promise<string> {
  const data = await (await page.request.get('/api/admin/clients?status=ACTIVE&pageSize=1')).json()
  if (data.total > 0) return data.items[0].id
  return (await apiPost(page.request, page, '/api/admin/clients', { name: 'E2E Test Client' })).id
}

/** Create a fresh project through the API (keeps tests independent of each other). */
export async function createProject(page: Page, name = `E2E Project ${Date.now()}`): Promise<string> {
  const clientId = await ensureClient(page)
  return (await apiPost(page.request, page, '/api/admin/projects', { clientId, name, designType: 'LOGO' })).id
}

/** Create a client with a unique name through the API; returns its id. */
export async function createClient(page: Page, name = `E2E Client ${Date.now()}`): Promise<string> {
  return (await apiPost(page.request, page, '/api/admin/clients', { name })).id
}

/** Create a project for a specific client; returns its id. */
export async function createProjectForClient(page: Page, clientId: string, name: string): Promise<string> {
  return (await apiPost(page.request, page, '/api/admin/projects', { clientId, name, designType: 'LOGO' })).id
}

/** Add a deliverable without a service; returns its id. */
export async function createDeliverable(page: Page, projectId: string, name: string, includedRounds = 2): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`,
    { name, quantity: 1, includedRounds, extraRoundPrice: 0 })).id
}

/** Walk a deliverable through the given statuses in order. */
export async function moveDeliverable(page: Page, deliverableId: string, ...statuses: string[]) {
  for (const to of statuses) {
    await apiPost(page.request, page, `/api/admin/deliverables/${deliverableId}/status`, { to })
  }
}

/** Create a task in a project through the API; returns its id. */
export async function createTask(
  page: Page,
  projectId: string,
  body: { title: string; status?: string; dueDate?: string },
): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/projects/${projectId}/tasks`, body)).id
}
