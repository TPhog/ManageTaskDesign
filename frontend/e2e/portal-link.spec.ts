import { test, expect } from '@playwright/test'
import { createClient, expectToast } from './helpers'

test.describe('Portal link (admin)', () => {
  test('designer creates, copies, regenerates and revokes a client portal link', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await page.goto('/admin')
    const name = `Portal Link E2E ${Date.now()}`
    await createClient(page, name)

    await page.goto('/admin/clients')
    await page.getByRole('textbox', { name: 'Tìm khách hàng' }).fill(name)
    await page.getByRole('button', { name: `Link portal của ${name}` }).click()
    const dialog = page.getByRole('dialog', { name: 'Link portal' })
    await expect(dialog.getByText('Chưa có link')).toBeVisible()

    await dialog.getByRole('button', { name: 'Tạo link', exact: true }).click()
    await expectToast(page, 'Đã tạo link portal')
    const first = await dialog.getByLabel('Link gửi khách').inputValue()
    expect(first).toMatch(/\/p\/[A-Za-z0-9_-]{43}$/)
    await expect(dialog.getByText('Đang hoạt động')).toBeVisible()

    await dialog.getByRole('button', { name: 'Copy tin nhắn mẫu' }).click()
    await expectToast(page, 'Đã copy tin nhắn mẫu')
    expect(await page.evaluate(() => navigator.clipboard.readText())).toContain(first)

    await dialog.getByRole('button', { name: 'Tạo lại', exact: true }).click()
    await dialog.getByRole('button', { name: 'Tạo link mới' }).click()
    await expectToast(page, 'Đã tạo link mới')
    const second = await dialog.getByLabel('Link gửi khách').inputValue()
    expect(second).toMatch(/\/p\/[A-Za-z0-9_-]{43}$/)
    expect(second).not.toBe(first)

    await dialog.getByRole('button', { name: 'Thu hồi', exact: true }).click()
    await dialog.getByRole('button', { name: 'Thu hồi link' }).click()
    await expectToast(page, 'Đã thu hồi link portal')
    await expect(dialog.getByText('Chưa có link')).toBeVisible()
    await expect(dialog.getByLabel('Link gửi khách')).toHaveCount(0)
  })
})
