import { test, expect } from '@playwright/test'
import { login, TEST_EMAIL, TEST_PASSWORD } from './helpers'

test.describe('Authentication', () => {
  test('login page shows correct form', async ({ page }) => {
    await page.goto('/admin/login')
    await expect(page.getByRole('heading', { name: /đăng nhập/i })).toBeVisible()
    await expect(page.getByLabel(/email/i)).toBeVisible()
    await expect(page.getByLabel(/mật khẩu/i)).toBeVisible()
    await expect(page.getByRole('button', { name: /đăng nhập/i })).toBeVisible()
  })

  test('wrong password shows error', async ({ page }) => {
    await page.goto('/admin/login')
    await page.getByLabel(/email/i).fill(TEST_EMAIL)
    await page.getByLabel(/mật khẩu/i).fill('wrongpassword')
    await page.getByRole('button', { name: /đăng nhập/i }).click()
    // Should stay on login page
    await expect(page).toHaveURL(/\/admin\/login/)
  })

  test('correct credentials log in and redirect to admin', async ({ page }) => {
    await login(page)
    await expect(page).toHaveURL(/\/admin/)
    // Should show admin layout with sidebar
    await expect(page.getByText('Dự án')).toBeVisible()
    await expect(page.getByText('Khách hàng')).toBeVisible()
  })

  test('logout clears session and redirects to login', async ({ page }) => {
    await login(page)
    await page.getByRole('button', { name: /đăng xuất/i }).click()
    await expect(page).toHaveURL(/\/admin\/login/)
  })

  test('unauthenticated access to admin redirects to login', async ({ page }) => {
    // No cookies set — go directly
    await page.goto('/admin/projects')
    await expect(page).toHaveURL(/\/admin\/login/)
  })
})
