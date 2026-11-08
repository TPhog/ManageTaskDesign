import { test, expect } from '@playwright/test'
import { expectToast, selectOption } from './helpers'

test.describe('Clients', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/clients')
    await expect(page.getByRole('heading', { name: 'Khách hàng', level: 1 })).toBeVisible()
  })

  test('clients page shows toolbar and create button', async ({ page }) => {
    await expect(page.getByRole('textbox', { name: 'Tìm khách hàng' })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Lọc theo trạng thái' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Thêm khách' }).first()).toBeVisible()
  })

  test('can create a new client', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm khách' }).first().click()
    await expect(page.getByRole('dialog', { name: 'Thêm khách hàng' })).toBeVisible()

    const clientName = `Test Client ${Date.now()}`
    await page.getByLabel('Tên khách').fill(clientName)
    await page.getByLabel('Công ty').fill('Test Company')
    await page.getByLabel('Số điện thoại').fill('0901234567')
    await page.getByRole('button', { name: 'Tạo khách' }).click()

    await expectToast(page, 'Đã tạo khách hàng')
    await expect(page.getByRole('cell', { name: clientName, exact: true })).toBeVisible()
  })

  test('requires a client name', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm khách' }).first().click()
    await page.getByRole('button', { name: 'Tạo khách' }).click()
    await expect(page.getByText('Vui lòng nhập tên khách')).toBeVisible()
  })

  test('can search clients by name', async ({ page }) => {
    await page.getByRole('textbox', { name: 'Tìm khách hàng' }).fill('nonexistent-xyz-12345')
    await expect(page.getByText('Không tìm thấy khách phù hợp')).toBeVisible()
  })

  test('can filter clients by status', async ({ page }) => {
    await selectOption(page, 'Lọc theo trạng thái', 'Đang hoạt động')
    await expect(page.getByRole('combobox', { name: 'Lọc theo trạng thái' })).toHaveText('Đang hoạt động')
    const rows = page.locator('tbody tr')
    for (let i = 0; i < Math.min(await rows.count(), 3); i++) {
      await expect(rows.nth(i)).toContainText('Đang hoạt động')
    }
  })
})
