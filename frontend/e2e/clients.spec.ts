import { test, expect } from '@playwright/test'
import { createClient, expectToast, selectOption } from './helpers'
import { archiveClient } from './cleanup'

test.describe('Clients', () => {
  const clientIds: string[] = []
  test.afterEach(async ({ page }) => {
    for (const id of clientIds.splice(0)) await archiveClient(page, id)
  })

  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/clients')
    await expect(page.getByRole('heading', { name: 'Khách hàng', level: 1 })).toBeVisible()
  })

  test('clients page shows toolbar and create button', async ({ page }) => {
    await expect(page.getByRole('textbox', { name: 'Tìm khách hàng' })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Lọc theo trạng thái' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Thêm khách' }).first()).toBeVisible()
  })

  test('creating a client opens its page', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm khách' }).first().click()
    await expect(page.getByRole('dialog', { name: 'Thêm khách hàng' })).toBeVisible()

    const clientName = `Test Client ${Date.now()}`
    await page.getByLabel('Tên khách').fill(clientName)
    await page.getByLabel('Công ty').fill('Test Company')
    await page.getByLabel('Số điện thoại').fill('0901234567')
    await page.getByRole('button', { name: 'Tạo khách' }).click()

    await expectToast(page, 'Đã tạo khách hàng')
    // does not depend on where the new row lands in a list of hundreds of clients
    await expect(page).toHaveURL(/\/admin\/clients\/[0-9a-f-]{36}$/)
    clientIds.push(page.url().split('/').pop()!)
    await expect(page.getByRole('heading', { name: clientName, level: 1 })).toBeVisible()
    await expect(page.getByText('0901234567')).toBeVisible()
  })

  test('finds a client by its unique name and edits it on the detail page', async ({ page }) => {
    const name = `Test Client ${Date.now()}`
    const id = await createClient(page, name)
    clientIds.push(id)
    await page.getByRole('textbox', { name: 'Tìm khách hàng' }).fill(name)
    await expect(page.locator('tbody tr')).toHaveCount(1)
    await page.getByRole('link', { name, exact: true }).click()

    await expect(page).toHaveURL(new RegExp(`/admin/clients/${id}$`))
    await expect(page.getByText('Chưa có dự án nào.')).toBeVisible()
    await page.getByRole('button', { name: 'Sửa thông tin' }).click()
    const sheet = page.getByRole('dialog', { name: 'Sửa thông tin khách' })
    await sheet.getByLabel('Số điện thoại').fill('0987654321')
    await sheet.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expectToast(page, 'Đã cập nhật khách hàng')
    await expect(page.getByText('0987654321')).toBeVisible()
  })

  test('long client lists are paged', async ({ page }) => {
    const total = (await (await page.request.get('/api/admin/clients?pageSize=1')).json()).total as number
    test.skip(total <= 20, 'needs more than 20 clients')
    const nav = page.getByRole('navigation', { name: 'Phân trang' })
    await expect(nav).toContainText(`1–20 / ${total} khách`)
    await nav.getByRole('button', { name: 'Trang sau' }).click()
    await expect(nav).toContainText(`21–${Math.min(40, total)} / ${total} khách`)
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

  test('portal link dialog previews the invitation before copying', async ({ page }) => {
    const name = `Test Client ${Date.now()}`
    const id = await createClient(page, name)
    clientIds.push(id)
    await page.getByRole('textbox', { name: 'Tìm khách hàng' }).fill(name)
    await page.getByRole('button', { name: `Link portal của ${name}` }).click()
    const dialog = page.getByRole('dialog', { name: 'Link portal' })
    await dialog.getByRole('button', { name: 'Tạo link', exact: true }).click()
    await expect(dialog.getByLabel('Tin nhắn mẫu (xem trước)')).toHaveValue(new RegExp(`^Chào ${name},`))
    await expect(dialog.getByRole('button', { name: 'Mở thử như khách' })).toBeVisible()
  })
})
