import { test, expect } from '@playwright/test'
import { createClient, createProject } from './helpers'

const PLACEHOLDER = 'Tìm khách, dự án, báo giá, công việc, yêu cầu…'

test.describe('Quick search (Ctrl+K)', () => {
  test('finds a project typed without diacritics and opens it with the keyboard', async ({ page }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const name = `E2E Tìm nhanh Đặng ${stamp}`
    const projectId = await createProject(page, name)
    await expect(page.locator('aside').getByRole('button', { name: /Tìm kiếm nhanh/ })).toBeVisible()

    await page.keyboard.press('Control+k')
    const input = page.getByPlaceholder(PLACEHOLDER)
    await expect(input).toBeFocused()
    await input.fill(`tim nhanh dang ${stamp}`)
    await expect(page.getByRole('option', { name: new RegExp(`Tìm nhanh Đặng ${stamp}`) })).toBeVisible()

    await page.keyboard.press('ArrowDown')
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}$`))
    await expect(page.getByPlaceholder(PLACEHOLDER)).toHaveCount(0)
  })

  test('the sidebar button opens search; a client result opens the client detail page', async ({ page }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientName = `E2E Khách Tìm ${stamp}`
    const clientId = await createClient(page, clientName)

    await page.locator('aside').getByRole('button', { name: /Tìm kiếm nhanh/ }).click()
    await page.getByPlaceholder(PLACEHOLDER).fill(`khach tim ${stamp}`)
    await page.getByRole('option', { name: new RegExp(clientName) }).click()

    await expect(page).toHaveURL(new RegExp(`/admin/clients/${clientId}$`))
  })

  test('explains the minimum length and the empty result', async ({ page }) => {
    await page.goto('/admin')
    // the shortcut is registered once the layout has mounted
    await expect(page.locator('aside').getByRole('button', { name: /Tìm kiếm nhanh/ })).toBeVisible()
    await page.keyboard.press('Control+k')
    const input = page.getByPlaceholder(PLACEHOLDER)
    await input.fill('x')
    await expect(page.getByText('Nhập ít nhất 2 ký tự để tìm.')).toBeVisible()
    await input.fill(`zz-khong-co-${Date.now()}`)
    await expect(page.getByText(/Không tìm thấy kết quả/)).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(input).toHaveCount(0)
  })
})
