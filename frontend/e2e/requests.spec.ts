import { test, expect } from '@playwright/test'
import { createProject, expectToast, selectOption } from './helpers'
import { pickOption } from './comboboxHelpers'
import { createQuotaDeliverable, createRequest } from './revisionHelpers'

test.describe('Requests inbox', () => {
  test('records a request, triages it with the suggested billing and uses the quota', async ({ page }) => {
    await page.goto('/admin')
    const projectName = `E2E Requests ${Date.now()}`
    const projectId = await createProject(page, projectName)
    await createQuotaDeliverable(page, projectId, 'Logo E2E', 1)

    await page.goto('/admin/requests')
    await page.getByRole('button', { name: 'Ghi nhận yêu cầu' }).click()
    const dialog = page.getByRole('dialog', { name: 'Ghi nhận yêu cầu' })
    await pickOption(page, 'Dự án', projectName)
    await selectOption(page, 'Hạng mục (tuỳ chọn)', 'Logo E2E')
    await dialog.getByLabel('Nội dung yêu cầu').fill('Đổi màu chữ sang xanh navy')
    await dialog.getByRole('button', { name: 'Ghi nhận', exact: true }).click()
    await expectToast(page, 'Đã ghi nhận yêu cầu')

    const sheet = page.getByRole('dialog', { name: /Yêu cầu từ/ })
    await expect(sheet).toContainText('Đổi màu chữ sang xanh navy')
    await sheet.getByLabel('Mô tả mục 1').fill('Đổi màu chữ sang xanh navy')
    await expect(sheet.getByText('Logo E2E đã dùng 0/1 vòng → Trong quota')).toBeVisible()
    await sheet.getByRole('button', { name: 'Lưu & tạo vòng sửa' }).click()
    await expectToast(page, 'Đã tiếp nhận yêu cầu')
    await expect(sheet.getByText('Đã xử lý', { exact: true })).toBeVisible()
    await expect(sheet).toContainText('Vòng 1 — Logo E2E')

    await page.goto(`/admin/projects/${projectId}`)
    await page.getByRole('tab', { name: /Deliverables/ }).click()
    await expect(page.getByText('1/1 vòng')).toBeVisible()
  })

  test('declines a request with a reason the client will see', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const content = `Làm thêm 10 banner miễn phí ${Date.now()}`
    const requestId = await createRequest(page, projectId, content)

    await page.goto(`/admin/requests/${requestId}`)
    const sheet = page.getByRole('dialog', { name: /Yêu cầu từ/ })
    await sheet.getByRole('button', { name: 'Từ chối', exact: true }).click()
    const reason = page.getByRole('dialog', { name: 'Từ chối yêu cầu' })
    await reason.getByLabel('Lý do (khách sẽ thấy)').fill('Ngoài phạm vi hợp đồng')
    await reason.getByRole('button', { name: 'Từ chối yêu cầu' }).click()
    await expectToast(page, 'Đã từ chối yêu cầu')
    await expect(sheet).toContainText('Lý do từ chối: Ngoài phạm vi hợp đồng')

    await page.goto('/admin/requests')
    await page.getByRole('tab', { name: 'Từ chối' }).click()
    await expect(page.getByText(content)).toBeVisible()
  })

  test('sidebar shows the number of new requests', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    await createRequest(page, projectId, 'Badge E2E')

    await page.goto('/admin')
    await expect(page.getByRole('link', { name: /Yêu cầu\s*\d+ yêu cầu mới/ })).toBeVisible()
  })
})
