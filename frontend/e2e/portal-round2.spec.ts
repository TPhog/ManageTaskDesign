import { test, expect } from '@playwright/test'
import { createClient, createDeliverable, createProjectForClient, moveDeliverable } from './helpers'
import { issuePortalLink, openAsClient } from './portalHelpers'
import { pngFile } from './imageHelpers'

// Plan 12 (BR-48 one review voice, CF-08 no floating button, CF-13 response time).
// The agree / transfer-reported flow (BR-76/77) lives in portal-quotation-request.spec.ts.
test.describe('Client portal — one review voice (plan 12)', () => {
  test('product status stays single-voiced through approve and change request', async ({ page, browser }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `R2 Review ${stamp}`)
    const projectName = `R2 Duyệt ${stamp}`
    const projectId = await createProjectForClient(page, clientId, projectName)
    for (const name of ['Logo', 'Menu A4']) {
      const d = await createDeliverable(page, projectId, name, 2)
      await moveDeliverable(page, d, 'IN_PROGRESS', 'IN_REVIEW')
    }
    const link = await issuePortalLink(page, clientId)

    const { context, page: portal } = await openAsClient(browser, link)
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.getByRole('region', { name: 'Việc bạn cần làm' })
      .getByRole('link', { name: new RegExp(`Duyệt 2 sản phẩm của ${projectName}`) }).click()
    const products = portal.getByRole('region', { name: 'Sản phẩm' })
    await expect(portal.getByText('2 chờ bạn duyệt')).toBeVisible()

    await portal.getByRole('button', { name: 'Duyệt Logo' }).click()
    await portal.getByRole('dialog', { name: 'Duyệt Logo?' }).getByRole('button', { name: 'Duyệt', exact: true }).click()
    const logo = products.getByRole('listitem').filter({ hasText: 'Logo' })
    await expect(logo).toContainText('Bạn đã duyệt — chờ xác nhận')
    await expect(logo).not.toContainText('Chờ bạn duyệt')
    await expect(portal.getByText('1 chờ bạn duyệt')).toBeVisible()

    await portal.getByRole('button', { name: 'Yêu cầu sửa Menu A4' }).click()
    const dialog = portal.getByRole('dialog', { name: 'Yêu cầu sửa Menu A4' })
    await dialog.getByLabel('Nội dung cần sửa').fill(`Đổi màu chữ ${stamp}`)
    await dialog.locator('#request-images').setInputFiles(pngFile('mau.png'))
    await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(dialog).toContainText('Đã gửi yêu cầu')
    await expect(dialog).toContainText(/Nhà thiết kế thường phản hồi/)
    await dialog.getByRole('button', { name: 'Xong' }).click()

    const menu = products.getByRole('listitem').filter({ hasText: 'Menu A4' })
    await expect(menu).toContainText('Đang sửa theo yêu cầu của bạn')
    await expect(portal.getByRole('button', { name: 'Duyệt Menu A4' })).toHaveCount(0)
    await expect(portal.getByText(/chờ bạn duyệt/)).toHaveCount(0)
    await expect(portal.locator('a.fixed')).toHaveCount(0)   // CF-08: no floating button over the content
    expect(await portal.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
    await context.close()
  })
})
