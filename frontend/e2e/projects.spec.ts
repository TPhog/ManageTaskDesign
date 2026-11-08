import { test, expect } from '@playwright/test'
import { ensureClient, expectToast, selectOption } from './helpers'

test.describe('Projects — List & Gantt', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/projects')
    await expect(page.getByRole('heading', { name: 'Dự án', level: 1 })).toBeVisible()
  })

  test('projects page shows list and gantt toggle buttons', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Danh sách' })).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByRole('button', { name: 'Timeline Gantt' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Tạo dự án' }).first()).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Lọc theo trạng thái' })).toHaveText('Tất cả trạng thái')
  })

  test('can switch to gantt view', async ({ page }) => {
    await page.getByRole('button', { name: 'Timeline Gantt' }).click()
    await expect(page.getByRole('button', { name: 'Timeline Gantt' })).toHaveAttribute('aria-pressed', 'true')
    await expect(
      page.getByRole('img', { name: 'Timeline dự án' }).or(page.getByText('Chưa có dự án nào có ngày bắt đầu / deadline')),
    ).toBeVisible()
  })

  test('can create a project without dates', async ({ page }) => {
    await ensureClient(page)
    await page.getByRole('button', { name: 'Tạo dự án' }).first().click()
    await expect(page.getByRole('dialog', { name: 'Tạo dự án mới' })).toBeVisible()

    const projectName = `E2E Project ${Date.now()}`
    await page.getByRole('combobox', { name: /Khách hàng/ }).click()
    await page.getByRole('option').first().click()
    await page.getByLabel(/Tên dự án/).fill(projectName)
    await selectOption(page, /Loại thiết kế/, 'Logo')
    await page.getByRole('dialog').getByRole('button', { name: 'Tạo dự án' }).click()

    await expectToast(page, 'Đã tạo dự án')
    // QA D-21: the new project opens on its Deliverables tab
    await expect(page).toHaveURL(/\/admin\/projects\/[0-9a-f-]{36}\?tab=deliverables$/)
    await expect(page.getByRole('heading', { name: projectName, level: 1 })).toBeVisible()
  })

  test('rejects a deadline before the start date', async ({ page }) => {
    await page.getByRole('button', { name: 'Tạo dự án' }).first().click()
    await page.getByLabel('Ngày bắt đầu').fill('2026-12-10')
    await page.getByLabel('Deadline').fill('2026-12-01')
    await page.getByRole('dialog').getByRole('button', { name: 'Tạo dự án' }).click()
    await expect(page.getByText('Deadline phải sau ngày bắt đầu')).toBeVisible()
  })

  test('can filter projects by status', async ({ page }) => {
    await selectOption(page, 'Lọc theo trạng thái', 'Nháp')
    const rows = page.locator('tbody tr')
    for (let i = 0; i < Math.min(await rows.count(), 3); i++) {
      await expect(rows.nth(i)).toContainText('Nháp')
    }
  })
})

test.describe('Project Detail', () => {
  test('project detail page shows tabs', async ({ page }) => {
    await page.goto('/admin/projects')
    const firstRow = page.locator('tbody tr').first()
    // wait for the list to load before deciding there is nothing to open
    await expect(firstRow.or(page.getByText('Chưa có dự án nào'))).toBeVisible()
    test.skip((await page.locator('tbody tr').count()) === 0, 'No projects to test detail view')
    const firstLink = firstRow.getByRole('link')

    await firstLink.click()
    await expect(page).toHaveURL(/\/admin\/projects\/[0-9a-f-]+/)
    await expect(page.getByRole('tab', { name: 'Tổng quan' })).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Brief' })).toBeVisible()
    await expect(page.getByRole('tab', { name: /Deliverables/ })).toBeVisible()
  })

  test('unknown route shows the 404 page', async ({ page }) => {
    await page.goto('/admin/does-not-exist')
    await expect(page.getByRole('heading', { name: 'Không tìm thấy trang' })).toBeVisible()
    await page.getByRole('link', { name: 'Về trang tổng quan' }).click()
    await expect(page).toHaveURL(/\/admin$/)
  })
})
