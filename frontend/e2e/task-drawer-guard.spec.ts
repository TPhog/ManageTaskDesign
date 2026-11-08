import { test, expect } from '@playwright/test'
import { createProject, createTask } from './helpers'

test.describe('Task drawer unsaved-changes guard (D-30)', () => {
  test('Esc with an edited description asks first; keep editing keeps the text, discard closes', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const title = `Guard ${Date.now()}`
    await createTask(page, projectId, { title })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('region', { name: 'Cột Cần làm' }).getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByLabel('Mô tả', { exact: true }).fill('Bản nháp chưa lưu')

    await page.keyboard.press('Escape')
    const confirm = page.getByRole('dialog').filter({ hasText: 'Bỏ thay đổi chưa lưu?' })
    await expect(confirm).toBeVisible()

    await confirm.getByRole('button', { name: 'Tiếp tục sửa' }).click()
    await expect(confirm).toBeHidden()
    await expect(drawer.getByLabel('Mô tả', { exact: true })).toHaveValue('Bản nháp chưa lưu')

    await page.keyboard.press('Escape')
    await expect(confirm).toBeVisible()
    await confirm.getByRole('button', { name: 'Bỏ thay đổi' }).click()
    await expect(drawer).toBeHidden()
  })

  test('a clean drawer closes on Esc without asking', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const title = `Clean ${Date.now()}`
    await createTask(page, projectId, { title })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('region', { name: 'Cột Cần làm' }).getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await expect(drawer).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(drawer).toBeHidden()
    await expect(page.getByRole('dialog').filter({ hasText: 'Bỏ thay đổi chưa lưu?' })).toHaveCount(0)
  })
})
