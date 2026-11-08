import type { Page } from '@playwright/test'
import { csrfToken } from './helpers'

/**
 * Archive a client the spec created so ACTIVE lists and pickers stop growing run after run.
 * Best effort: the data is kept (never deleted); a client with an IN_PROGRESS/ON_HOLD project stays active (BR-10).
 */
export async function archiveClient(page: Page, clientId: string) {
  await page.request.post(`/api/admin/clients/${clientId}/archive`, {
    headers: { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin },
  })
}
