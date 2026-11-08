import { test, expect, type Browser, type Page } from '@playwright/test'
import { createClient, createDeliverable, createProjectForClient, expectToast, moveDeliverable } from './helpers'

// POST /api/portal/session is limited to 10/min per IP: this file uses 6 — don't rerun it within a minute.

/** Designer side: open the client's portal-link dialog and create a link; returns the raw URL. */
async function createLinkInUi(page: Page, clientName: string): Promise<string> {
  await page.goto('/admin/clients')
  await page.getByRole('textbox', { name: 'Tìm khách hàng' }).fill(clientName)
  await page.getByRole('button', { name: `Link portal của ${clientName}` }).click()
  const dialog = page.getByRole('dialog', { name: 'Link portal' })
  await dialog.getByRole('button', { name: 'Tạo link', exact: true }).click()
  await expectToast(page, 'Đã tạo link portal')
  return dialog.getByLabel('Link gửi khách').inputValue()
}

/** The client's phone: a fresh context without the designer's cookies. */
async function openAsClient(browser: Browser, url: string) {
  const context = await browser.newContext({
    storageState: { cookies: [], origins: [] },
    viewport: { width: 390, height: 844 },
  })
  const page = await context.newPage()
  // 'commit': the landing page navigates on to /portal by itself as soon as the token is exchanged
  const response = await page.goto(url, { waitUntil: 'commit' })
  return { context, page, response }
}

test.describe('Client portal', () => {
  test('client opens the magic link and follows project progress', async ({ page, browser }) => {
    await page.goto('/admin')
    const clientName = `Portal E2E ${Date.now()}`
    const clientId = await createClient(page, clientName)
    const projectId = await createProjectForClient(page, clientId, 'Bộ nhận diện E2E')
    const logo = await createDeliverable(page, projectId, 'Logo chính', 3)
    await moveDeliverable(page, logo, 'IN_PROGRESS', 'IN_REVIEW', 'APPROVED', 'DELIVERED')
    await createDeliverable(page, projectId, 'Danh thiếp')

    const link = await createLinkInUi(page, clientName)
    const { context, page: portal, response } = await openAsClient(browser, link)
    expect(response?.headers()['referrer-policy']).toBe('no-referrer')

    await expect(portal).toHaveURL(/\/portal$/)
    await expect(portal.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()
    const card = portal.getByRole('link', { name: /Bộ nhận diện E2E/ })
    await expect(card).toContainText('1/2 hạng mục đã bàn giao')
    await card.click()

    await expect(portal).toHaveURL(new RegExp(`/portal/projects/${projectId}$`))
    await expect(portal.getByRole('heading', { name: 'Bộ nhận diện E2E', level: 1 })).toBeVisible()
    const deliverables = portal.getByRole('region', { name: 'Hạng mục' })
    await expect(deliverables.getByRole('listitem').filter({ hasText: 'Logo chính' })).toContainText('Đã bàn giao')
    await expect(deliverables.getByRole('listitem').filter({ hasText: 'Danh thiếp' })).toContainText('Chưa bắt đầu')
    await expect(portal.getByRole('region', { name: 'Tiến trình' })).toContainText('Logo chính: Đã bàn giao')
    await context.close()
  })

  test('revoked link and its open session stop working', async ({ page, browser }) => {
    await page.goto('/admin')
    const clientName = `Portal Revoke E2E ${Date.now()}`
    await createClient(page, clientName)
    const link = await createLinkInUi(page, clientName)

    const client = await openAsClient(browser, link)
    await expect(client.page.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()
    await expect(client.page.getByText('Chưa có dự án nào')).toBeVisible()

    const dialog = page.getByRole('dialog', { name: 'Link portal' })
    await dialog.getByRole('button', { name: 'Thu hồi', exact: true }).click()
    await dialog.getByRole('button', { name: 'Thu hồi link' }).click()
    await expectToast(page, 'Đã thu hồi link portal')

    await client.page.reload()
    await expect(client.page.getByText('Phiên xem đã hết hạn')).toBeVisible()
    await client.context.close()

    const again = await openAsClient(browser, link)
    await expect(again.page.getByText('Liên kết không hợp lệ hoặc đã hết hạn')).toBeVisible()
    await again.context.close()
  })

  test('regenerated link replaces the old one', async ({ page, browser }) => {
    await page.goto('/admin')
    const clientName = `Portal Regenerate E2E ${Date.now()}`
    await createClient(page, clientName)
    const oldLink = await createLinkInUi(page, clientName)
    const oldClient = await openAsClient(browser, oldLink)
    await expect(oldClient.page.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()

    const dialog = page.getByRole('dialog', { name: 'Link portal' })
    await dialog.getByRole('button', { name: 'Tạo lại', exact: true }).click()
    await dialog.getByRole('button', { name: 'Tạo link mới' }).click()
    await expectToast(page, 'Đã tạo link mới')
    const newLink = await dialog.getByLabel('Link gửi khách').inputValue()
    expect(newLink).not.toBe(oldLink)

    await oldClient.page.reload()
    await expect(oldClient.page.getByText('Phiên xem đã hết hạn')).toBeVisible()
    await oldClient.context.close()

    const stale = await openAsClient(browser, oldLink)
    await expect(stale.page.getByText('Liên kết không hợp lệ hoặc đã hết hạn')).toBeVisible()
    await stale.context.close()

    const fresh = await openAsClient(browser, newLink)
    await expect(fresh.page.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()
    await fresh.context.close()
  })
})
