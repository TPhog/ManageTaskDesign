import type { Page } from '@playwright/test'
import { apiPut } from './portalHelpers'

/** GET /api/admin/settings as returned — its shape is the PUT body (plus rowVersion). */
export type SettingsSnapshot = Record<string, unknown>

export async function readSettings(page: Page): Promise<SettingsSnapshot> {
  return (await page.request.get('/api/admin/settings')).json()
}

/** Put back what readSettings returned; re-reads rowVersion so a test's own save never causes a 409. */
export async function restoreSettings(page: Page, snapshot: SettingsSnapshot) {
  const current = await readSettings(page)
  await apiPut(page, '/api/admin/settings', { ...snapshot, rowVersion: current.rowVersion })
}
