import { test, expect, type Page } from '@playwright/test'
import { apiPost, createProject, csrfToken, expectToast, moveDeliverable, selectOption } from './helpers'

/** PUT with the CSRF double-submit token + Origin that CsrfMiddleware requires. */
async function apiPut(page: Page, url: string, data: unknown) {
  const res = await page.request.put(url, {
    data,
    headers: { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin },
  })
  expect(res.ok(), `${url} → ${res.status()}`).toBeTruthy()
}

/** Sending needs a receiving account (docs/06 §5); settings.spec may clear it, so set it before each test. */
async function configureBank(page: Page) {
  const settings = await (await page.request.get('/api/admin/settings')).json()
  await apiPut(page, '/api/admin/settings', {
    ...settings,
    bankBin: '970436',
    bankAccountNo: '0123456789',
    bankAccountName: 'NGUYEN VAN A',
  })
}

async function createService(page: Page, name: string, basePrice: number): Promise<string> {
  return (await apiPost(page.request, page, '/api/admin/services',
    { name, description: '', basePrice, defaultIncludedRounds: 2, extraRoundPrice: 100_000 })).id
}

async function addDeliverable(page: Page, projectId: string, name: string, serviceId: string): Promise<string> {
  return (await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name, quantity: 1, serviceId })).id
}

async function sendFromEditor(page: Page) {
  await page.getByRole('button', { name: 'Gửi báo giá' }).click()
  await page.getByRole('dialog', { name: 'Gửi báo giá?' }).getByRole('button', { name: 'Gửi', exact: true }).click()
  await expectToast(page, /Đã gửi báo giá [A-Z]+-\d{4}-\d{4}/)
}

test.describe('Quotations', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    await configureBank(page)
  })

  test('quotes the whole project, sends it and shows the VietQR', async ({ page }) => {
    const stamp = Date.now()
    const logo = await createService(page, `E2E Logo ${stamp}`, 2_000_000)
    const banner = await createService(page, `E2E Banner ${stamp}`, 500_000)
    const projectId = await createProject(page, `E2E Báo giá ${stamp}`)
    await addDeliverable(page, projectId, 'Logo', logo)
    await addDeliverable(page, projectId, 'Banner', banner)

    await page.goto(`/admin/projects/${projectId}`)
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await page.getByRole('button', { name: 'Tạo báo giá' }).first().click()
    await page.getByRole('dialog', { name: 'Tạo báo giá' }).getByRole('button', { name: 'Tạo nháp' }).click()

    await expectToast(page, 'Đã tạo báo giá nháp')
    await expect(page).toHaveURL(/\/admin\/quotations\/[0-9a-f-]+$/)
    await expect(page.getByLabel('Mô tả dòng 1')).toHaveValue('Logo')
    await expect(page.getByLabel('Đơn giá dòng 2')).toHaveValue('500000')

    await page.getByLabel('Giảm giá (₫)').fill('100000')
    await page.getByLabel('Cọc (%)').fill('50')
    await page.getByRole('button', { name: 'Lưu nháp' }).click()
    await expectToast(page, 'Đã lưu báo giá')
    await expect(page.getByTestId('draft-total')).toContainText('2.400.000')

    await sendFromEditor(page)
    await expect(page.getByTestId('snapshot-total')).toContainText('2.400.000')
    await expect(page.getByTestId('vietqr')).toBeVisible()
    await expect(page.getByTestId('qr-amount')).toContainText('1.200.000')
    await expect(page.getByTestId('qr-memo')).toHaveText(/^[A-Z]+\d{8} COC$/)
    await page.getByRole('radio', { name: 'Thanh toán toàn bộ' }).click()
    await expect(page.getByTestId('qr-amount')).toContainText('2.400.000')
    await expect(page.getByTestId('qr-memo')).toHaveText(/^[A-Z]+\d{8} TT$/)

    await page.getByRole('button', { name: 'Đánh dấu đã chấp nhận' }).click()
    await page.getByRole('dialog', { name: 'Đánh dấu đã chấp nhận' }).getByRole('button', { name: 'Xác nhận' }).click()
    await expectToast(page, 'Đã đánh dấu chấp nhận')

    await page.getByRole('button', { name: 'Đã nhận cọc' }).click()
    await page.getByRole('dialog', { name: 'Đã nhận cọc' }).getByRole('button', { name: 'Lưu' }).click()
    await expectToast(page, 'Đã cập nhật thanh toán')
    await expect(page.getByTestId('qr-amount')).toContainText('1.200.000')
    await expect(page.getByTestId('qr-memo')).toHaveText(/^[A-Z]+\d{8} CONLAI$/)
  })

  test('quotes only the selected finished deliverables with its own QR', async ({ page }) => {
    const stamp = Date.now()
    const logo = await createService(page, `E2E Logo ${stamp}`, 2_000_000)
    const banner = await createService(page, `E2E Banner ${stamp}`, 500_000)
    const projectId = await createProject(page, `E2E Báo giá hạng mục ${stamp}`)
    const logoId = await addDeliverable(page, projectId, 'Logo', logo)
    const bannerId = await addDeliverable(page, projectId, 'Banner', banner)
    await addDeliverable(page, projectId, 'Poster', banner)   // stays NOT_STARTED → not offered
    await moveDeliverable(page, logoId, 'IN_PROGRESS', 'IN_REVIEW', 'APPROVED', 'DELIVERED')
    await moveDeliverable(page, bannerId, 'IN_PROGRESS', 'IN_REVIEW', 'APPROVED')

    await page.goto(`/admin/projects/${projectId}`)
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await page.getByRole('button', { name: 'Tạo báo giá' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Tạo báo giá' })
    await dialog.getByRole('radio', { name: /Chọn hạng mục đã xong/ }).check()
    await expect(dialog.getByRole('checkbox')).toHaveCount(2)
    await dialog.getByRole('checkbox', { name: /Logo/ }).check()
    await dialog.getByRole('button', { name: 'Tạo nháp' }).click()

    await expectToast(page, 'Đã tạo báo giá nháp')
    await expect(page.getByLabel('Mô tả dòng 1')).toHaveValue('Logo')
    await expect(page.getByLabel('Mô tả dòng 2')).toHaveCount(0)
    await page.getByLabel('Cọc (%)').fill('0')
    await sendFromEditor(page)

    await expect(page.getByRole('radiogroup', { name: 'Chọn khoản thanh toán' })).toHaveCount(0)   // no deposit → full only
    await expect(page.getByTestId('qr-amount')).toContainText('2.000.000')
    await expect(page.getByTestId('qr-memo')).toHaveText(/^[A-Z]+\d{8} TT$/)
  })
})

test.describe('Quotations page', () => {
  test('lists quotations across projects and filters by effective status', async ({ page }) => {
    const stamp = Date.now()
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E Danh sách BG ${stamp}`)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/quotations`,
      { kind: 'INITIAL', title: `BG danh sách ${stamp}`, fromDeliverables: false })

    await page.getByRole('link', { name: 'Báo giá' }).first().click()
    await expect(page).toHaveURL(/\/admin\/quotations$/)
    await expect(page.getByRole('heading', { name: 'Báo giá', level: 1 })).toBeVisible()
    const row = page.locator('tbody tr').filter({ hasText: `BG danh sách ${stamp}` })
    await expect(row).toBeVisible()

    await selectOption(page, 'Lọc trạng thái', 'Đã gửi')
    await expect(row).toHaveCount(0)
    await selectOption(page, 'Lọc trạng thái', 'Nháp')
    await expect(row).toBeVisible()

    await row.getByRole('link').first().click()
    await expect(page.getByRole('heading', { name: 'Soạn báo giá' })).toBeVisible()
  })
})
