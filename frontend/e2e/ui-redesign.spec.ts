import { test, expect } from '@playwright/test'
import { ADMIN_ROUTES, WIDTHS, capture, overflowPx } from './uiHelpers'

test.describe('UI redesign (P16) — Studio Bento admin', () => {
  test('paper theme, self-hosted fonts, no Google Fonts request', async ({ page }) => {
    const external: string[] = []
    page.on('request', r => { if (/fonts\.(googleapis|gstatic)\.com/.test(r.url())) external.push(r.url()) })
    await page.goto('/admin')
    await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible()
    await page.evaluate(() => document.fonts.ready)
    expect(external).toEqual([])
    expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe('rgb(245, 241, 234)')
    expect(await page.evaluate(() => getComputedStyle(document.body).fontFamily)).toContain('Be Vietnam Pro')
    expect(await page.evaluate(() => document.fonts.check('400 16px "Be Vietnam Pro"', 'Tổng quan'))).toBe(true)
  })

  test('page titles use the display face; sidebar marks the current page in ink', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto('/admin/projects')
    const h1 = page.getByRole('heading', { name: 'Dự án', level: 1 })
    await expect(h1).toBeVisible()
    expect(await h1.evaluate(el => getComputedStyle(el).fontFamily)).toContain('Bricolage Grotesque Variable')
    const active = page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Dự án' })
    expect(await active.evaluate(el => getComputedStyle(el).backgroundColor)).toBe('rgb(28, 26, 23)')
  })

  test('dashboard is a 12-column bento at 1440 and one column without sideways scroll at 390', async ({ page }, testInfo) => {
    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto('/admin')
    const today = page.getByRole('region', { name: 'Hôm nay của bạn' })
    await expect(today).toBeVisible()
    const columns = (el: Element) => getComputedStyle(el.parentElement!.parentElement!).gridTemplateColumns.split(' ').length
    expect(await today.evaluate(columns)).toBe(12)
    const schedule = page.getByRole('region', { name: 'Lịch làm việc 7 ngày' })
    const days = schedule.locator('section[aria-label]:visible')
    if (!(await schedule.getByText(/Tuần này chưa có việc nào/).isVisible())) expect(await days.count()).toBeGreaterThanOrEqual(7)
    await expect(page.getByRole('region', { name: 'Tiền đã thu và công nợ' }).locator('[data-tone="inverse"]')).toBeVisible()
    await capture(page, testInfo, 'dashboard-bento-1440')

    await page.setViewportSize({ width: 390, height: 844 })
    await page.reload()
    await expect(today).toBeVisible()
    expect(await today.evaluate(columns)).toBe(1)
    expect(await overflowPx(page)).toBeLessThanOrEqual(0)
    await capture(page, testInfo, 'dashboard-bento-390')
  })

  for (const width of WIDTHS) {
    test(`captures every admin page at ${width}px`, async ({ page }, testInfo) => {
      await page.setViewportSize({ width, height: 900 })
      for (const r of ADMIN_ROUTES) {
        await page.goto(r.path)
        await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible()
        await page.evaluate(() => document.fonts.ready)
        const px = await overflowPx(page)
        testInfo.annotations.push({ type: `overflow-${width}`, description: `${r.name}: ${px}px` })
        await capture(page, testInfo, `${r.name}-${width}`)
      }
    })
  }
})
