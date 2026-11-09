import { test, expect, request as pwRequest } from '@playwright/test'
import { API_BASE, csrfToken, expectToast } from './helpers'
import { archiveClient } from './cleanup'

/** Plan 13: public form submission -> "Khách mới" inbox -> one-click conversion. Runs against the dev API (form token minimum fill time is 3 s). */
async function submitPublicIntake(slug: string, fields: Record<string, string>) {
  const anon = await pwRequest.newContext({ baseURL: API_BASE })
  try {
    const form = await anon.get(`/api/public/intake/${slug}`)
    expect(form.ok()).toBeTruthy()
    const { formToken } = await form.json() as { formToken: string }
    await new Promise(r => setTimeout(r, 3500))
    const res = await anon.post(`/api/public/intake/${slug}`, { multipart: { website: '', formToken, ...fields } })
    expect(res.status(), await res.text()).toBeLessThan(300)
  } finally {
    await anon.dispose()
  }
}

test.describe('Khách mới inbox', () => {
  test('converts a public-form submission into client + project + brief', async ({ page }) => {
    await page.goto('/admin')
    const csrf = { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin }
    const before = await (await page.request.get('/api/admin/settings/intake')).json() as { enabled: boolean }
    const link = await (await page.request.put('/api/admin/settings/intake', { data: { enabled: true }, headers: csrf })).json() as { slug: string }

    const stamp = Date.now()
    const name = `Chị E2E ${stamp}`
    const phone = `09${String(stamp).slice(-8)}`
    const note = `Cần thiết kế menu quán cà phê ${stamp}`
    try {
      await submitPublicIntake(link.slug, { contactName: name, contactPhone: phone, note })

      await page.goto('/admin/intake')
      const row = page.getByRole('link', { name: new RegExp(name) })
      await expect(row).toBeVisible()
      await expect(page.getByRole('navigation', { name: 'Điều hướng chính' }).getByLabel(/khách mới$/)).toBeVisible()
      await row.click()

      const sheet = page.getByRole('dialog', { name: `Khách mới: ${name}` })
      await expect(sheet).toContainText(note)
      await expect(sheet.getByRole('link', { name: `Gọi ${phone}` })).toBeVisible()
      await sheet.getByRole('button', { name: 'Tạo khách & dự án' }).click()

      const dialog = page.getByRole('dialog', { name: 'Tạo khách & dự án' })
      await expect(dialog.getByLabel('Tên khách')).toHaveValue(name)
      await expect(dialog.getByLabel('Tên dự án')).toHaveValue(`Yêu cầu — ${name}`)
      await dialog.getByRole('button', { name: 'Tạo khách & dự án' }).click()
      await expectToast(page, 'Đã tạo khách & dự án')

      await expect(page).toHaveURL(/\/admin\/projects\/[0-9a-f-]{36}$/)
      const projectId = page.url().split('/').pop()!
      await expect(page.getByRole('heading', { name: `Yêu cầu — ${name}` })).toBeVisible()

      // brief v1 holds what the client wrote
      await page.goto(`/admin/projects/${projectId}?tab=brief`)
      await expect(page.getByRole('region', { name: 'Nội dung brief' })).toContainText(note)

      // the client exists with the phone from the form
      const clients = await (await page.request.get(`/api/admin/clients?q=${encodeURIComponent(name)}`)).json() as { items: { id: string; name: string }[] }
      const created = clients.items.find(c => c.name === name)
      expect(created).toBeTruthy()

      // the inbox moved it to "Đã chuyển thành dự án"
      await page.goto('/admin/intake')
      await page.getByRole('tab', { name: 'Đã chuyển thành dự án' }).click()
      await expect(page.getByRole('link', { name: new RegExp(name) })).toBeVisible()

      await archiveClient(page, created!.id)
    } finally {
      if (!before.enabled) await page.request.put('/api/admin/settings/intake', { data: { enabled: false }, headers: csrf })
    }
  })

  test('rejects and deletes a submission', async ({ page }) => {
    await page.goto('/admin')
    const csrf = { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin }
    const before = await (await page.request.get('/api/admin/settings/intake')).json() as { enabled: boolean }
    const link = await (await page.request.put('/api/admin/settings/intake', { data: { enabled: true }, headers: csrf })).json() as { slug: string }
    const stamp = Date.now()
    const name = `Anh E2E ${stamp}`
    try {
      await submitPublicIntake(link.slug, { contactName: name, contactPhone: `09${String(stamp).slice(-8)}`, note: 'Làm banner Tết' })
      await page.goto('/admin/intake')
      await page.getByRole('link', { name: new RegExp(name) }).click()
      const sheet = page.getByRole('dialog', { name: `Khách mới: ${name}` })
      await sheet.getByRole('button', { name: 'Từ chối' }).click()
      await page.getByRole('dialog', { name: 'Từ chối hồ sơ này?' }).getByRole('button', { name: 'Từ chối' }).click()
      await expectToast(page, 'Đã từ chối hồ sơ')
      await expect(sheet).toContainText('Đã từ chối')

      await sheet.getByRole('button', { name: 'Xoá' }).click()
      await page.getByRole('dialog', { name: 'Xoá hồ sơ này?' }).getByRole('button', { name: 'Xoá' }).click()
      await expectToast(page, 'Đã xoá hồ sơ')
      await page.getByRole('tab', { name: 'Đã từ chối' }).click()
      await expect(page.getByRole('link', { name: new RegExp(name) })).toHaveCount(0)
    } finally {
      if (!before.enabled) await page.request.put('/api/admin/settings/intake', { data: { enabled: false }, headers: csrf })
    }
  })
})
