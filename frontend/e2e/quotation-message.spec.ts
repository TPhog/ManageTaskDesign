import { test, expect, type Page } from '@playwright/test'
import { apiPost, createClient, createProjectForClient, expectToast } from './helpers'
import { configureBank, createService } from './portalHelpers'

async function sendFromEditor(page: Page) {
  await page.getByRole('button', { name: 'Gửi báo giá' }).click()
  await page.getByRole('dialog', { name: 'Gửi báo giá?' }).getByRole('button', { name: 'Gửi', exact: true }).click()
  await expectToast(page, /Đã gửi báo giá [A-Z]+-\d{4}-\d{4}/)
}

test.describe('Quotation message template', () => {
  test('after sending, the designer creates a portal link and copies the docs/08 message', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await page.goto('/admin')
    await configureBank(page)
    const stamp = Date.now()
    const clientName = `E2E Tin nhắn ${stamp}`
    const clientId = await createClient(page, clientName)
    const projectId = await createProjectForClient(page, clientId, `E2E Tin nhắn dự án ${stamp}`)
    const service = await createService(page, `E2E Logo tin nhắn ${stamp}`, 2_000_000)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name: 'Logo', quantity: 1, serviceId: service })
    const draft = await apiPost(page.request, page, `/api/admin/projects/${projectId}/quotations`, { kind: 'INITIAL', fromDeliverables: true })

    await page.goto(`/admin/quotations/${draft.id}`)
    await sendFromEditor(page)

    const dialog = page.getByRole('dialog', { name: 'Đã gửi báo giá' })
    await expect(dialog).toBeVisible()
    const message = dialog.getByLabel('Tin nhắn gửi khách')
    await expect(message).toHaveValue(new RegExp(`^Chào anh/chị ${clientName}, em gửi báo giá [A-Z]+-\\d{4}-\\d{4} \\(2\\.000\\.000`))
    await expect(message).toHaveValue(/mở link portal em đã gửi trước đó/)

    await dialog.getByRole('button', { name: 'Tạo link portal' }).click()
    await expectToast(page, 'Đã tạo link portal')
    await expect(message).toHaveValue(/quét QR tại: \S+\/p\/[A-Za-z0-9_-]{43}$/)

    await dialog.getByRole('button', { name: 'Copy tin nhắn', exact: true }).click()
    await expectToast(page, 'Đã copy tin nhắn mẫu')
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(await message.inputValue())

    // Reopened later: the client now has a link (shown only once), so the message points to the old link
    await page.keyboard.press('Escape')
    await expect(dialog).toHaveCount(0)
    await page.getByRole('button', { name: 'Tin nhắn mẫu', exact: true }).click()
    const again = page.getByRole('dialog', { name: 'Tin nhắn mẫu' })
    await expect(again.getByLabel('Tin nhắn gửi khách')).toHaveValue(/mở link portal em đã gửi trước đó/)
    await expect(again.getByRole('button', { name: 'Tạo link portal' })).toHaveCount(0)

    // Portal turned off: no "old link" message, the dialog points to the client page instead
    await page.keyboard.press('Escape')
    await apiPost(page.request, page, `/api/admin/clients/${clientId}/portal/disable`, {})
    await page.reload()
    await page.getByRole('button', { name: 'Tin nhắn mẫu', exact: true }).click()
    const off = page.getByRole('dialog', { name: 'Tin nhắn mẫu' })
    await expect(off.getByText('Portal của khách đang tắt')).toBeVisible()
    await expect(off.getByLabel('Tin nhắn gửi khách')).not.toHaveValue(/link portal em đã gửi trước đó/)
    await expect(off.getByRole('button', { name: 'Copy tin nhắn', exact: true })).toBeDisabled()
  })
})
