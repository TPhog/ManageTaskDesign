import { test, expect } from '@playwright/test'
import { createClient, createProjectForClient } from './helpers'
import { issuePortalLink, openAsClient } from './portalHelpers'
import { capture, overflowPx } from './uiHelpers'

// POST /api/portal/session is limited to 10/min per IP: this file opens one session.
test.describe('UI redesign (P16) — Editorial portal', () => {
  test('editorial theme on a phone: cream paper, serif masthead, 44px targets, no sideways scroll', async ({ page, browser }, testInfo) => {
    await page.goto('/admin')
    const clientId = await createClient(page, `Editorial E2E ${Date.now()}`)
    const projectId = await createProjectForClient(page, clientId, 'Bộ nhận diện Editorial')
    const url = await issuePortalLink(page, clientId)
    const { context, page: portal } = await openAsClient(browser, url)

    await expect(portal).toHaveURL(/\/portal$/)
    await expect(portal.getByRole('heading', { name: 'Dự án của bạn' })).toBeVisible()
    await portal.evaluate(() => document.fonts.ready)
    expect(await portal.evaluate(() => document.documentElement.dataset.theme)).toBe('editorial')
    expect(await portal.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe('rgb(243, 238, 227)')
    expect(await portal.evaluate(() => getComputedStyle(document.body).fontFamily)).toContain('Public Sans Variable')
    const wordmark = portal.getByRole('banner').getByRole('link').first()
    expect(await wordmark.evaluate(el => getComputedStyle(el.querySelector('span')!).fontFamily)).toContain('Fraunces Variable')

    for (const target of [
      portal.getByRole('button', { name: 'Thoát' }),
      ...['Dự án', 'Báo giá', 'Yêu cầu'].map(n => portal.getByRole('navigation', { name: 'Điều hướng portal' }).getByRole('link', { name: n })),
    ]) expect((await target.boundingBox())!.height).toBeGreaterThanOrEqual(44)

    expect(await overflowPx(portal)).toBeLessThanOrEqual(0)
    await capture(portal, testInfo, 'portal-home-390')
    for (const path of [`/portal/projects/${projectId}`, '/portal/quotations', '/portal/requests', '/portal/requests/new']) {
      await portal.goto(path)
      await expect(portal.getByRole('heading', { level: 1 }).first()).toBeVisible()
      await portal.evaluate(() => document.fonts.ready)
      expect(await overflowPx(portal), `${path} scrolls sideways at 390px`).toBeLessThanOrEqual(0)
    }
    await portal.setViewportSize({ width: 1440, height: 900 })
    await capture(portal, testInfo, 'portal-home-1440')
    await context.close()
  })
})
