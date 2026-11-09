import { test, expect } from '@playwright/test'
import { apiPost, createClient, createDeliverable, createProjectForClient, moveDeliverable } from './helpers'
import { apiPut, issuePortalLink, openAsClient } from './portalHelpers'
import { readSettings, restoreSettings, type SettingsSnapshot } from './settingsHelpers'
import { archiveClient } from './cleanup'

test.describe('Portal QA (C-04, C-06, C-12, C-14)', () => {
  let saved: SettingsSnapshot
  const clientIds: string[] = []

  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    saved = await readSettings(page)
    await apiPut(page, '/api/admin/settings', { ...saved, contactPhone: '0901234567' })
  })

  test.afterEach(async ({ page }) => {
    await restoreSettings(page, saved)
    for (const id of clientIds.splice(0)) await archiveClient(page, id)
  })

  test('client sees what waits for them, designer messages by day and how to reach the studio', async ({ page, browser }) => {
    const tag = `${Date.now()}`
    const clientId = await createClient(page, `E2E Portal QA ${tag}`)
    clientIds.push(clientId)
    const projectId = await createProjectForClient(page, clientId, `E2E Biển hiệu ${tag}`)
    const sign = await createDeliverable(page, projectId, 'Biển hiệu')
    await moveDeliverable(page, sign, 'IN_PROGRESS', 'IN_REVIEW')
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/progress-updates`, { message: `Đã gửi bản phác thảo ${tag}` })
    const link = await issuePortalLink(page, clientId)
    const { context, page: portal } = await openAsClient(browser, link)

    await expect(portal.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()
    const card = portal.getByRole('region', { name: 'Dự án của bạn' }).getByRole('link', { name: new RegExp(`E2E Biển hiệu ${tag}`) })
    await expect(card).toContainText('1 sản phẩm chờ bạn duyệt')
    await expect(portal.getByRole('link', { name: 'Gọi' })).toHaveAttribute('href', 'tel:0901234567')
    await expect(portal.getByRole('link', { name: 'Nhắn Zalo' })).toHaveAttribute('href', 'https://zalo.me/0901234567')

    await card.click()
    const timeline = portal.getByRole('region', { name: 'Hoạt động gần đây' })
    await expect(timeline.getByRole('heading', { name: 'Hôm nay' })).toBeVisible()
    await expect(timeline.getByText('Nhà thiết kế nhắn')).toBeVisible()
    await expect(timeline.getByText(`Đã gửi bản phác thảo ${tag}`)).toBeVisible()
    await expect(portal.getByRole('region', { name: 'Sản phẩm' })).toContainText('Chờ bạn duyệt')

    await portal.getByRole('button', { name: 'Thoát' }).click()
    await portal.getByRole('button', { name: 'Thoát', exact: true }).last().click()
    await expect(portal.getByText('Bạn đã thoát')).toBeVisible()
    await expect(portal.getByRole('link', { name: 'Gọi' })).toHaveAttribute('href', 'tel:0901234567')
    await context.close()
  })
})
