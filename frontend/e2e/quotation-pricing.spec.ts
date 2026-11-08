import { test, expect } from '@playwright/test'
import { apiPost, createDeliverable, createProject, expectToast } from './helpers'
import { configureBank, createService } from './portalHelpers'
import { pickOption } from './comboboxHelpers'
import { triageItems } from './revisionHelpers'

test.describe('Quotation pricing (plan 09)', () => {
  test('prices an additional quotation at creation and turns the new item into a deliverable on acceptance', async ({ page }) => {
    const stamp = Date.now()
    const serviceName = `Sticker E2E ${stamp}`
    const itemName = `Thiết kế sticker dán ly ${stamp}`
    const scopeName = `Thêm bản tiếng Anh ${stamp}`
    await page.goto('/admin')
    await configureBank(page)
    await apiPost(page.request, page, '/api/admin/services', {
      name: serviceName, description: '', basePrice: 300_000, defaultIncludedRounds: 2, extraRoundPrice: 100_000,
      taskTemplate: ['Phác thảo sticker'],
    })
    const projectId = await createProject(page, `E2E Phát sinh ${stamp}`)
    await triageItems(page, projectId, null, [
      { description: itemName, type: 'NEW_DELIVERABLE' },
      { description: scopeName, type: 'SCOPE_CHANGE' },
    ])

    await page.goto(`/admin/projects/${projectId}?tab=revisions`)
    const tab = page.getByRole('tabpanel')
    await tab.getByRole('checkbox', { name: `Chọn ${itemName}` }).check()
    await tab.getByRole('checkbox', { name: `Chọn ${scopeName}` }).check()
    await tab.getByRole('button', { name: 'Tạo báo giá phát sinh (2)' }).click()

    const dialog = page.getByRole('dialog', { name: 'Tạo báo giá phát sinh' })
    await expect(dialog.getByText('Còn 2 dòng 0 ₫')).toBeVisible()
    await pickOption(page, `Dịch vụ mẫu cho ${itemName}`, serviceName)
    await expect(dialog.getByLabel(`Đơn giá ${itemName}`)).toHaveValue('300.000')
    await dialog.getByLabel(`Đơn giá ${scopeName}`).fill('450000')
    await expect(dialog.getByText(/Còn \d dòng 0 ₫/)).toHaveCount(0)
    await dialog.getByRole('button', { name: 'Tạo báo giá nháp' }).click()

    await expectToast(page, 'Đã tạo báo giá phát sinh')
    await expect(page).toHaveURL(/\/admin\/quotations\/[0-9a-f-]{36}$/)
    const quotationId = page.url().split('/').pop()!
    const draft = await (await page.request.get(`/api/admin/quotations/${quotationId}`)).json()
    expect(draft.items.map((i: { description: string; unitPrice: number }) => [i.description, i.unitPrice]))
      .toEqual([[serviceName, 300000], [scopeName, 450000]])

    await apiPost(page.request, page, `/api/admin/quotations/${quotationId}/send`, { rowVersion: draft.rowVersion })
    await page.reload()
    await page.getByRole('button', { name: 'Đánh dấu đã chấp nhận' }).click()
    const accept = page.getByRole('dialog', { name: 'Đánh dấu đã chấp nhận' })
    await expect(accept.getByText('Sẽ thêm 1 hạng mục mới vào dự án')).toBeVisible()
    await expect(accept.getByText(itemName)).toBeVisible()
    await expect(accept.getByText('Chưa bắt đầu')).toBeVisible()
    await accept.getByRole('button', { name: 'Xác nhận' }).click()
    await expectToast(page, 'thêm 1 hạng mục mới')

    await page.goto(`/admin/projects/${projectId}?tab=deliverables`)
    await expect(page.getByRole('tabpanel').getByText(itemName)).toBeVisible()
    await page.goto(`/admin/projects/${projectId}?tab=revisions`)
    await expect(page.getByRole('tabpanel').getByRole('link', { name: `Đã thành hạng mục: ${itemName}` })).toBeVisible()
  })

  test('prices an unpriced deliverable, fills a line from a service and names 0 ₫ lines before sending', async ({ page }) => {
    const stamp = Date.now()
    const poster = `Poster E2E ${stamp}`
    await page.goto('/admin')
    await configureBank(page)
    await createService(page, poster, 800_000)
    const projectId = await createProject(page, `E2E Giá gốc ${stamp}`)
    await createDeliverable(page, projectId, 'Thiệp mời', 2)
    await createDeliverable(page, projectId, 'Menu A5', 2)

    await page.goto(`/admin/projects/${projectId}`)
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await page.getByRole('button', { name: 'Tạo báo giá' }).first().click()
    const create = page.getByRole('dialog', { name: 'Tạo báo giá' })
    await create.getByLabel('Đơn giá Thiệp mời').fill('450000')
    await create.getByRole('button', { name: 'Tạo nháp' }).click()

    await expectToast(page, 'Đã tạo báo giá nháp')
    await expect(page.getByLabel('Đơn giá dòng 1')).toHaveValue('450.000')
    await expect(page.getByText('1 dòng có đơn giá 0 ₫. Nhập đơn giá hoặc bấm “Chọn từ dịch vụ”')).toBeVisible()

    await page.getByRole('button', { name: 'Thêm dòng' }).click()
    await pickOption(page, 'Chọn từ dịch vụ cho dòng 3', poster)
    await expect(page.getByLabel('Mô tả dòng 3')).toHaveValue(poster)
    await expect(page.getByLabel('Đơn giá dòng 3')).toHaveValue('800.000')

    await page.getByRole('button', { name: 'Gửi báo giá' }).click()
    const confirm = page.getByRole('dialog', { name: 'Gửi báo giá?' })
    await expect(confirm).toContainText('1 dòng có đơn giá 0 ₫:')
    await expect(confirm).toContainText('• Menu A5')
    await expect(confirm.getByRole('button', { name: 'Vẫn gửi' })).toBeVisible()
    await confirm.getByRole('button', { name: 'Huỷ' }).click()
  })
})
