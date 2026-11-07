import { test, expect } from '@playwright/test'
import { login, expectToast } from './helpers'

test.describe('Clients', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
    await page.getByRole('link', { name: /khách hàng/i }).click()
    await expect(page).toHaveURL(/\/admin\/clients/)
  })

  test('clients page loads with table headers', async ({ page }) => {
    await expect(page.getByText('Tên')).toBeVisible()
    await expect(page.getByText('Trạng thái')).toBeVisible()
    await expect(page.getByRole('button', { name: /thêm khách/i })).toBeVisible()
  })

  test('can create a new client', async ({ page }) => {
    await page.getByRole('button', { name: /thêm khách/i }).click()

    // Sheet opens
    await expect(page.getByText('Thêm khách hàng')).toBeVisible()

    const clientName = `Test Client ${Date.now()}`
    await page.getByLabel(/tên khách/i).fill(clientName)
    await page.getByLabel(/công ty/i).fill('Test Company')
    await page.getByLabel(/số điện thoại/i).fill('0901234567')

    await page.getByRole('button', { name: /tạo khách/i }).click()
    await expectToast(page, 'Đã tạo')

    // Client should appear in table
    await expect(page.getByText(clientName)).toBeVisible()
  })

  test('can search clients by name', async ({ page }) => {
    await page.getByPlaceholder(/tìm theo tên/i).fill('nonexistent-xyz-12345')
    await expect(page.getByText(/không tìm thấy/i)).toBeVisible()
  })

  test('can filter clients by status', async ({ page }) => {
    await page.getByRole('combobox').selectOption({ label: 'Đang hoạt động' })
    // Should show only active clients or empty state
    const rows = page.locator('tbody tr')
    const count = await rows.count()
    if (count > 0) {
      // All visible rows should show "Đang hoạt động" badge
      for (let i = 0; i < Math.min(count, 3); i++) {
        await expect(rows.nth(i).getByText('Đang hoạt động')).toBeVisible()
      }
    }
  })
})
