import { type Page, expect } from '@playwright/test'

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
