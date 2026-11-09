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
