import { test, expect } from '@playwright/test'
import { login, TEST_USERNAME } from './helpers'

// These tests exercise login itself, so start without the shared session.
test.use({ storageState: { cookies: [], origins: [] } })

test.describe('Authentication', () => {
  test('login page shows correct form', async ({ page }) => {
    await page.goto('/admin/login')
    await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
    await expect(page.getByLabel('Tên đăng nhập')).toBeVisible()
    await expect(page.getByLabel('Mật khẩu')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible()
  })

  test('wrong password shows error', async ({ page }) => {
    await page.goto('/admin/login')
    await page.getByLabel('Tên đăng nhập').fill(TEST_USERNAME)
    await page.getByLabel('Mật khẩu').fill('wrongpassword')
    await page.getByRole('button', { name: 'Đăng nhập' }).click()
    await expect(page.getByRole('alert')).toContainText('không đúng')
    await expect(page).toHaveURL(/\/admin\/login/)
  })

  test('correct credentials log in and redirect to admin', async ({ page }) => {
    await login(page)
    const nav = page.getByRole('navigation', { name: 'Điều hướng chính' })
    await expect(nav.getByRole('link', { name: 'Dự án' })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Khách hàng' })).toBeVisible()
  })

  test('logout clears session and redirects to login', async ({ page }) => {
    await login(page)
    await page.getByRole('button', { name: 'Đăng xuất' }).click()
    await expect(page).toHaveURL(/\/admin\/login/)
    await page.goto('/admin/projects')
    await expect(page).toHaveURL(/\/admin\/login/)
  })

  test('unauthenticated access to admin redirects to login', async ({ page }) => {
    await page.goto('/admin/projects')
    await expect(page).toHaveURL(/\/admin\/login/)
  })
})
