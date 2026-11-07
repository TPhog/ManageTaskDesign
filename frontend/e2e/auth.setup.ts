import { test as setup } from '@playwright/test'
import { login, STORAGE_STATE } from './helpers'

// Log in once and share the session: /api/auth/login is rate limited to 5 requests/min per IP.
setup('authenticate designer', async ({ page }) => {
  await login(page)
  await page.context().storageState({ path: STORAGE_STATE })
})
