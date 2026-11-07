import { type Page, expect } from '@playwright/test'

export const TEST_EMAIL = 'designer@example.com'
export const TEST_PASSWORD = 'password123456'
export const API_BASE = 'http://localhost:5261'

export async function login(page: Page) {
  await page.goto('/admin/login')
  await page.getByLabel(/email/i).fill(TEST_EMAIL)
  await page.getByLabel(/mật khẩu/i).fill(TEST_PASSWORD)
  await page.getByRole('button', { name: /đăng nhập/i }).click()
  await expect(page).toHaveURL(/\/admin$/)
}

export async function ensureLoggedIn(page: Page) {
  const res = await page.request.get(`${API_BASE}/api/auth/me`)
  if (res.status() !== 200) {
    await login(page)
  }
}

/** Wait for a toast with given text substring */
export async function expectToast(page: Page, text: string) {
  await expect(page.locator('[data-sonner-toast]').filter({ hasText: text })).toBeVisible({ timeout: 5000 })
}
