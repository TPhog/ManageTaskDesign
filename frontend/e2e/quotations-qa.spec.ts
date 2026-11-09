import { test, expect, type Page } from '@playwright/test'
import { apiPost, createDeliverable, createProject, expectToast } from './helpers'
import { configureBank, createService } from './portalHelpers'

async function openCreateDialog(page: Page, projectId: string) {
  await page.goto(`/admin/projects/${projectId}?tab=quotations`)
  await page.getByRole('button', { name: 'Tạo báo giá' }).first().click()
  return page.getByRole('dialog', { name: 'Tạo báo giá' })
}

/** One reusable service per dev DB instead of a new one every run. */
async function ensureService(page: Page, name: string, basePrice: number): Promise<string> {
  const list = (await (await page.request.get('/api/admin/services?includeInactive=true')).json()) as { id: string; name: string }[]
  return list.find(s => s.name === name)?.id ?? (await createService(page, name, basePrice))
}

test.describe('Quotations QA (D-02, D-03, D-04, D-12)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    await configureBank(page)
  })

  test('a deliverable without a service warns, its 0 ₫ line is flagged and sending asks twice', async ({ page }) => {
    const projectId = await createProject(page, `E2E BG 0đ ${Date.now()}`)
    await createDeliverable(page, projectId, 'Menu A4')

    const dialog = await openCreateDialog(page, projectId)
    await expect(dialog).toContainText('Đơn giá hạng mục chưa có dịch vụ mẫu')   // plan 09: priced here instead of warned about
    await expect(dialog.getByLabel('Đơn giá Menu A4')).toBeVisible()
    await dialog.getByRole('button', { name: 'Tạo nháp' }).click()
    await expectToast(page, 'Đã tạo báo giá nháp')

    await expect(page.getByText('1 dòng có đơn giá 0 ₫')).toBeVisible()
    await page.getByRole('button', { name: 'Gửi báo giá' }).click()
    const send = page.getByRole('dialog', { name: 'Gửi báo giá?' })
    await expect(send).toContainText('Tổng tiền đang là 0 ₫.')
    await expect(send.getByRole('button', { name: 'Vẫn gửi' })).toBeVisible()
    await send.getByRole('button', { name: 'Huỷ' }).click()

    // D-12: thousands separators once the field loses focus
    const price = page.getByLabel('Đơn giá dòng 1')
    await price.fill('2000000')
    await page.getByLabel('Mô tả dòng 1').click()
    await expect(price).toHaveValue('2.000.000')
    await expect(page.getByTestId('draft-total')).toContainText('2.000.000')
    await expect(page.getByText('1 dòng có đơn giá 0 ₫')).toHaveCount(0)

    // keep the dev DB tidy: drop the draft
    await page.getByRole('button', { name: 'Xóa báo giá' }).click()
    await page.getByRole('dialog', { name: 'Xóa báo giá nháp?' }).getByRole('button', { name: 'Xóa' }).click()
    await expectToast(page, 'Đã xóa báo giá nháp')
  })

  test('a deliverable already on a sent quotation needs an explicit acknowledgement', async ({ page }) => {
    const stamp = Date.now()
    const service = await ensureService(page, 'E2E Logo QA', 1_500_000)
    const projectId = await createProject(page, `E2E BG trùng ${stamp}`)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name: 'Logo chính', quantity: 1, serviceId: service })
    const draft = await apiPost(page.request, page, `/api/admin/projects/${projectId}/quotations`, { kind: 'INITIAL', fromDeliverables: true })
    const detail = await (await page.request.get(`/api/admin/quotations/${draft.id}`)).json()
    const sent = await apiPost(page.request, page, `/api/admin/quotations/${draft.id}/send`, { rowVersion: detail.rowVersion })

    const dialog = await openCreateDialog(page, projectId)
    await expect(dialog).toContainText(`Logo chính (${sent.number} · đã gửi)`)
    await expect(dialog.getByRole('button', { name: 'Tạo nháp' })).toBeDisabled()
    await dialog.getByRole('checkbox', { name: 'Tôi vẫn muốn báo giá lại các hạng mục này' }).check()
    await expect(dialog.getByRole('button', { name: 'Tạo nháp' })).toBeEnabled()
    await dialog.getByRole('button', { name: 'Huỷ' }).click()

    await page.getByRole('tab', { name: /Hạng mục/ }).click()
    await expect(page.getByRole('link', { name: `${sent.number} · đã gửi` })).toBeVisible()
  })
})
