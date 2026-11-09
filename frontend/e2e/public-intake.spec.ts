import { test, expect, type Page } from '@playwright/test'
import { csrfToken, expectToast } from './helpers'
import { apiPut } from './portalHelpers'

/** Plan 13 "/f/:slug": an anonymous visitor (no designer/portal cookie) sends a template request. Runs against the dev API. */

async function intakeLink(page: Page) {
  await page.goto('/admin')
  return (await (await page.request.get('/api/admin/settings/intake')).json()) as { enabled: boolean; slug: string | null }
}

/** Removes the submissions this spec created from the "Khách mới" inbox (dev DB). */
async function deleteSubmissions(page: Page, contactName: string) {
  const list = await (await page.request.get('/api/admin/intake?status=NEW&pageSize=100')).json() as { items: { id: string; contactName: string }[] }
  for (const item of list.items.filter(i => i.contactName === contactName)) {
    await page.request.delete(`/api/admin/intake/${item.id}`, {
      headers: { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin },
    })
  }
}

test.describe('Public intake form /f/:slug', () => {
  test('a new client fills the Poster template, survives FORM_EXPIRED without retyping and sees the thank-you', async ({ page, browser }) => {
    const before = await intakeLink(page)
    const { slug } = await apiPut(page, '/api/admin/settings/intake', { enabled: true }) as { slug: string }
    const name = `Chị E2E ${Date.now()}`
    const anon = await browser.newContext({ storageState: { cookies: [], origins: [] }, viewport: { width: 390, height: 844 } })
    try {
      const visitor = await anon.newPage()
      const otherApiCalls: string[] = []
      visitor.on('request', r => { if (/\/api\/(admin|portal)\//.test(r.url())) otherApiCalls.push(r.url()) })
      let formLoads = 0
      let posts = 0
      await visitor.route(`**/api/public/intake/${slug}`, async route => {
        if (route.request().method() === 'GET') {
          formLoads++
          return route.continue()
        }
        posts++
        // the first post meets an expired single-use token; the page must fetch a new one and keep the answers
        if (posts === 1) return route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ code: 'FORM_EXPIRED' }) })
        return route.continue()
      })

      const res = await visitor.goto(`/f/${slug}`)
      expect(res?.headers()['referrer-policy']).toBe('no-referrer')
      await expect(visitor.getByRole('heading', { level: 1 })).toBeVisible()
      await expect(visitor.getByRole('region', { name: 'Bạn cần thiết kế gì?' })).toBeVisible()
      // honeypot: outside the accessibility tree and the tab order
      await expect(visitor.getByRole('textbox', { name: 'Website' })).toHaveCount(0)
      await expect(visitor.locator('input[name="website"]')).toHaveAttribute('tabindex', '-1')

      await visitor.getByRole('radio', { name: /^Poster/ }).check()
      await visitor.getByRole('textbox', { name: 'Mục đích poster' }).fill('Giới thiệu món mới')
      await visitor.getByRole('radio', { name: 'Đăng online', exact: true }).check()
      await visitor.getByRole('radio', { name: 'A3 (297 × 420 mm)', exact: true }).check()
      await visitor.getByRole('textbox', { name: 'Tiêu đề chính' }).fill('Bánh mì nướng muối ớt')
      await visitor.getByRole('textbox', { name: 'Tên của bạn' }).fill(name)
      await visitor.getByRole('textbox', { name: 'Số điện thoại / Zalo' }).fill('0909 123 456')

      await visitor.getByRole('button', { name: 'Gửi yêu cầu' }).click()
      await expectToast(visitor, 'Biểu mẫu đã hết hạn')
      await expect.poll(() => formLoads).toBe(2)
      await expect(visitor.getByRole('textbox', { name: 'Tiêu đề chính' })).toHaveValue('Bánh mì nướng muối ớt')
      await expect(visitor.getByRole('textbox', { name: 'Tên của bạn' })).toHaveValue(name)

      await visitor.getByRole('button', { name: 'Gửi yêu cầu' }).click()
      await expect(visitor.getByText('Đã gửi yêu cầu')).toBeVisible({ timeout: 10_000 })
      await expect(visitor.getByText(`Cảm ơn ${name}!`, { exact: false })).toBeVisible()
      expect(posts).toBe(2)
      expect(otherApiCalls).toEqual([])   // no session needed, none asked for

      // stored for the designer (the server answered 204, not the honeypot/too-fast fake)
      const inbox = await (await page.request.get('/api/admin/intake?status=NEW&pageSize=100')).json() as { items: { contactName: string; templateName: string | null }[] }
      expect(inbox.items.find(i => i.contactName === name)?.templateName).toBe('Poster')

      // "Gửi yêu cầu khác" starts over with a fresh token
      await visitor.getByRole('button', { name: 'Gửi yêu cầu khác' }).click()
      await expect(visitor.getByRole('textbox', { name: 'Tên của bạn' })).toHaveValue('')
      await expect.poll(() => formLoads).toBe(3)
    } finally {
      await anon.close()
      await deleteSubmissions(page, name)
      await apiPut(page, '/api/admin/settings/intake', { enabled: before.enabled })
    }
  })

  test('a link that was turned off says so and offers nothing else', async ({ page, browser }) => {
    const before = await intakeLink(page)
    const { slug } = await apiPut(page, '/api/admin/settings/intake', { enabled: true }) as { slug: string }
    await apiPut(page, '/api/admin/settings/intake', { enabled: false })
    const anon = await browser.newContext({ storageState: { cookies: [], origins: [] }, viewport: { width: 390, height: 844 } })
    try {
      const visitor = await anon.newPage()
      await visitor.goto(`/f/${slug}`)
      await expect(visitor.getByRole('heading', { name: 'Link không còn hoạt động' })).toBeVisible()
      await expect(visitor.getByRole('button')).toHaveCount(0)
    } finally {
      await anon.close()
      await apiPut(page, '/api/admin/settings/intake', { enabled: before.enabled })
    }
  })
})
