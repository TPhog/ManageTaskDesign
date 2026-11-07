import { test, expect } from '@playwright/test'
import { login, expectToast } from './helpers'

test.describe('Projects — List & Gantt', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
    await page.getByRole('link', { name: /dự án/i }).click()
    await expect(page).toHaveURL(/\/admin\/projects/)
  })

  test('projects page shows list and gantt toggle buttons', async ({ page }) => {
    await expect(page.getByTitle('Danh sách')).toBeVisible()
    await expect(page.getByTitle('Timeline Gantt')).toBeVisible()
    await expect(page.getByRole('button', { name: /tạo dự án/i })).toBeVisible()
  })

  test('can switch to gantt view', async ({ page }) => {
    await page.getByTitle('Timeline Gantt').click()
    // Gantt SVG should appear
    await expect(page.locator('svg')).toBeVisible()
  })

  test('can create a project (requires at least 1 active client)', async ({ page }) => {
    // First ensure there's a client by going to clients page
    await page.getByRole('link', { name: /khách hàng/i }).click()
    const clientCount = await page.locator('tbody tr').count()
    if (clientCount === 0) {
      // Create a client first
      await page.getByRole('button', { name: /thêm khách/i }).click()
      await page.getByLabel(/tên khách/i).fill('E2E Test Client')
      await page.getByRole('button', { name: /tạo khách/i }).click()
      await expectToast(page, 'Đã tạo')
    }

    // Go back to projects
    await page.getByRole('link', { name: /dự án/i }).click()
    await page.getByRole('button', { name: /tạo dự án/i }).click()

    // Dialog opens
    await expect(page.getByRole('dialog')).toBeVisible()

    // Fill project details
    const projectName = `E2E Project ${Date.now()}`
    await page.getByLabel(/tên dự án/i).fill(projectName)

    // Select design type
    await page.getByRole('combobox', { name: /loại thiết kế/i }).click()
    await page.getByRole('option', { name: /logo/i }).click()

    // Select first available client
    await page.getByRole('combobox', { name: /khách hàng/i }).click()
    await page.getByRole('option').first().click()

    await page.getByRole('button', { name: /tạo/i }).click()
    await expectToast(page, 'thành công')

    // Project appears in list
    await expect(page.getByText(projectName)).toBeVisible()
  })

  test('can filter projects by status', async ({ page }) => {
    await page.getByRole('combobox').filter({ hasText: /trạng thái/i }).click()
    await page.getByRole('option', { name: /nháp/i }).click()
    // Status filter applied — table shows draft projects
    const rows = page.locator('tbody tr')
    const count = await rows.count()
    if (count > 0) {
      await expect(rows.first().getByText('Nháp')).toBeVisible()
    }
  })
})

test.describe('Project Detail', () => {
  test('project detail page shows tabs', async ({ page }) => {
    await login(page)
    await page.getByRole('link', { name: /dự án/i }).click()

    const rows = page.locator('tbody tr')
    const count = await rows.count()
    test.skip(count === 0, 'No projects to test detail view')

    // Click first project's detail link
    await rows.first().getByRole('link').click()
    await expect(page).toHaveURL(/\/admin\/projects\/[0-9a-f-]+/)

    // Tabs should be visible
    await expect(page.getByRole('tab', { name: /tổng quan/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /brief/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /deliverable/i })).toBeVisible()
  })
})
