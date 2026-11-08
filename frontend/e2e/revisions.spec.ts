import { test, expect } from '@playwright/test'
import { createProject, expectToast, selectOption } from './helpers'
import { createQuotaDeliverable, triageRequest } from './revisionHelpers'

test.describe('Revisions tab', () => {
  test('quota meter, scope guard and an additional quotation from a chargeable item', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const logo = await createQuotaDeliverable(page, projectId, 'Logo E2E', 1)
    await triageRequest(page, projectId, logo, ['Sửa vòng 1'])               // 1/1 → IN_QUOTA
    await triageRequest(page, projectId, logo, ['Thêm tagline dưới logo'])   // quota used → CHARGEABLE

    await page.goto(`/admin/projects/${projectId}?tab=revisions`)
    const tab = page.getByRole('tabpanel')
    await expect(tab.getByText('1/1 vòng')).toBeVisible()
    await expect(tab.getByText('Vòng 2', { exact: true })).toBeVisible()
    await expect(tab.getByText('1 mục tính phí chưa báo giá')).toBeVisible()

    // BR-60: chargeable work without an accepted quotation needs a reason
    await tab.getByRole('button', { name: 'Thao tác Thêm tagline dưới logo' }).click()
    await page.getByRole('menuitem', { name: 'Bắt đầu làm' }).click()
    const guard = page.getByRole('dialog', { name: 'Mục này tính phí nhưng báo giá chưa được chấp nhận' })
    await guard.getByLabel('Lý do vẫn làm').fill('Khách đã đồng ý qua Zalo')
    await guard.getByRole('button', { name: 'Vẫn bắt đầu' }).click()
    await expectToast(page, 'Đã cập nhật trạng thái mục')
    await expect(tab.getByText('Làm trước báo giá: Khách đã đồng ý qua Zalo')).toBeVisible()

    await tab.getByRole('checkbox', { name: 'Chọn Thêm tagline dưới logo' }).check()
    await tab.getByRole('button', { name: /Tạo báo giá phát sinh/ }).click()
    await expectToast(page, 'Đã tạo báo giá phát sinh')
    await expect(page).toHaveURL(/\/admin\/quotations\/[0-9a-f-]{36}$/)
    const quotationId = page.url().split('/').pop()!
    const quotation = await (await page.request.get(`/api/admin/quotations/${quotationId}`)).json()
    expect(quotation.kind).toBe('ADDITIONAL')
    expect(quotation.items.map((i: { description: string; unitPrice: number }) => [i.description, i.unitPrice]))
      .toEqual([['Vòng sửa thứ 2 — Logo E2E', 200000]])
  })

  test('adds a manual round and creates a task from an item', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    await createQuotaDeliverable(page, projectId, 'Banner E2E', 2)

    await page.goto(`/admin/projects/${projectId}?tab=revisions`)
    await page.getByRole('button', { name: 'Thêm vòng sửa' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Thêm vòng sửa' })
    await selectOption(page, 'Hạng mục vòng 1', 'Banner E2E')
    await dialog.getByLabel('Mô tả mục 1').fill('Đổi nền banner')
    await dialog.getByRole('button', { name: 'Thêm vòng', exact: true }).click()
    await expectToast(page, 'Đã thêm vòng sửa')
    const tab = page.getByRole('tabpanel')
    await expect(tab.getByText('1/2 vòng')).toBeVisible()

    await tab.getByRole('button', { name: 'Thao tác Đổi nền banner' }).click()
    await page.getByRole('menuitem', { name: 'Tạo công việc' }).click()
    await expectToast(page, 'Đã tạo công việc')
    await expect(tab.getByRole('link', { name: '1 công việc' })).toBeVisible()
  })

  test('progress update shows in what the client sees', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)

    await page.goto(`/admin/projects/${projectId}`)
    await page.getByLabel('Cập nhật tiến độ cho khách').fill('Đã xong bản phác thảo logo')
    await page.getByRole('button', { name: 'Gửi cập nhật' }).click()
    await expectToast(page, 'Đã gửi cập nhật cho khách')
    await expect(page.getByRole('tabpanel').getByText('Đã xong bản phác thảo logo')).toBeVisible()
  })
})
