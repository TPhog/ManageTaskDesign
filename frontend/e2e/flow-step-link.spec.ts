import { test, expect } from '@playwright/test'
import { createClient, createProjectForClient } from './helpers'
import { quotedProject } from './flowHelpers'

test.describe('Flow dialog steps opened by link (?step=)', () => {
  test('?step=START_PROJECT opens the start dialog once and leaves a clean URL', async ({ page }) => {
    const projectId = await quotedProject(page, Date.now())

    await page.goto(`/admin/projects/${projectId}?step=START_PROJECT`)
    await expect(page.getByRole('dialog', { name: 'Bắt đầu dự án' })).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}$`))

    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog', { name: 'Bắt đầu dự án' })).toHaveCount(0)
    await page.reload()
    await expect(page.getByRole('region', { name: 'Bước tiếp theo' }).getByRole('button', { name: 'Bắt đầu dự án' })).toBeVisible()
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  test('a step the flow does not offer is dropped without opening anything', async ({ page }) => {
    await page.goto('/admin')
    const clientId = await createClient(page, `Khách step ${Date.now()}`)
    const projectId = await createProjectForClient(page, clientId, `Dự án step ${Date.now()}`)

    await page.goto(`/admin/projects/${projectId}?step=COMPLETE_PROJECT`)
    await expect(page.getByRole('region', { name: 'Bước tiếp theo' })).toContainText('Thêm hạng mục')
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}$`))
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })
})
