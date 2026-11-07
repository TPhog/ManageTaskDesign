import { test, expect } from '@playwright/test'
import { login, expectToast } from './helpers'

test.describe('Settings', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
    await page.getByRole('link', { name: /cài đặt/i }).click()
    await expect(page).toHaveURL(/\/admin\/settings/)
  })

  test('settings page loads with form fields', async ({ page }) => {
    await expect(page.getByLabel(/tên hiển thị/i)).toBeVisible()
  })

  test('can update display name', async ({ page }) => {
    const field = page.getByLabel(/tên hiển thị/i)
    await field.clear()
    await field.fill('CamCam Design Studio Test')
    await page.getByRole('button', { name: /lưu/i }).click()
    await expectToast(page, 'Đã lưu')
  })

  test('services page loads and shows create button', async ({ page }) => {
    await page.getByRole('link', { name: /dịch vụ/i }).click()
    await expect(page).toHaveURL(/\/admin\/services/)
    await expect(page.getByRole('button', { name: /thêm dịch vụ/i })).toBeVisible()
  })

  test('can create a new service', async ({ page }) => {
    await page.getByRole('link', { name: /dịch vụ/i }).click()
    await page.getByRole('button', { name: /thêm dịch vụ/i }).click()
    // Fill form
    await page.getByLabel(/tên dịch vụ/i).fill('Logo Design E2E Test')
    await page.getByLabel(/giá gốc/i).fill('2000000')
    await page.getByLabel(/số vòng sửa/i).fill('3')
    await page.getByLabel(/giá vòng thêm/i).fill('200000')
    await page.getByRole('button', { name: /lưu/i }).click()
    await expectToast(page, 'thành công')
    await expect(page.getByText('Logo Design E2E Test')).toBeVisible()
  })
})
